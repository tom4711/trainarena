using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrainArena.Contracts;
using TrainArena.Data;
using TrainArena.Game;
using TrainArena.Game.PowerUps;

namespace TrainArena.Hubs;

/// <summary>
/// SignalR hub for live quiz rooms.
/// </summary>
public sealed class GameHub : Hub
{
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> QuestionEndTimers =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, CancellationTokenSource> AutoAdvanceTimers =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly GameSessionStore _sessions;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;

    public GameHub(
        GameSessionStore sessions,
        IHubContext<GameHub> hubContext,
        IServiceScopeFactory scopeFactory)
    {
        _sessions = sessions;
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
    }

    public static string RoomGroup(string code) => $"room:{code.ToUpperInvariant()}";

    public async Task CreateRoom(
        Guid quizId,
        PowerUpConfigDto? powerUpConfig = null,
        AutoAdvanceConfigDto? autoAdvance = null)
    {
        PowerUpRoomConfig? roomConfig = null;
        if (powerUpConfig is not null)
        {
            var (mapped, mapError) = MapPowerUpConfig(powerUpConfig);
            if (mapError is not null)
            {
                await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage(mapError));
                return;
            }

            roomConfig = mapped!;
            var validationError = roomConfig.Validate();
            if (validationError is not null)
            {
                await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage(validationError));
                return;
            }
        }

        AutoAdvanceOptions? autoAdvanceOptions = null;
        if (autoAdvance is not null)
        {
            autoAdvanceOptions = new AutoAdvanceOptions
            {
                Enabled = autoAdvance.Enabled,
                DelaySeconds = autoAdvance.DelaySeconds,
            };
            var autoError = autoAdvanceOptions.Validate();
            if (autoError is not null)
            {
                await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage(autoError));
                return;
            }
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quiz = await db.Quizzes
            .AsNoTracking()
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == quizId);

        if (quiz is null || quiz.Questions.Count == 0)
        {
            await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage("Quiz nicht gefunden oder leer"));
            return;
        }

        var questions = quiz.Questions
            .OrderBy(q => q.SortOrder)
            .Select(QuizRules.ToDemoQuestion)
            .ToList();

        var session = _sessions.Create(Context.ConnectionId, quizId, roomConfig, autoAdvanceOptions);
        session.SetQuestions(questions);

        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(session.Code));
        await Clients.Caller.SendAsync("RoomCreated", new RoomCreatedMessage(session.Code));
        await Clients.Caller.SendAsync("LobbyState", ToLobbyState(session));
    }

    public async Task JoinRoom(string code, string nickname)
    {
        if (!_sessions.TryGet(code, out var session) || session is null)
        {
            await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage("Raum nicht gefunden"));
            return;
        }

        var (ok, error) = session.TryJoin(nickname, Context.ConnectionId);
        if (!ok)
        {
            await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage(error ?? "Beitritt fehlgeschlagen"));
            return;
        }

        _sessions.BindConnection(Context.ConnectionId, session.Code);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(session.Code));

        await Clients.Group(RoomGroup(session.Code)).SendAsync(
            "PlayerJoined",
            new PlayerJoinedMessage(nickname.Trim(), session.ConnectedPlayerCount));
        await Clients.Group(RoomGroup(session.Code)).SendAsync("LobbyState", ToLobbyState(session));
        await SendInventoryUpdateToCaller(session, nickname.Trim());
    }

    /// <summary>Re-attach a player after SignalR reconnect (same room + nickname).</summary>
    public async Task RejoinRoom(string code, string nickname)
    {
        if (!_sessions.TryGet(code, out var session) || session is null)
        {
            await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage("Raum nicht gefunden"));
            return;
        }

        var (ok, error) = session.TryRejoin(nickname, Context.ConnectionId);
        if (!ok)
        {
            await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage(error ?? "Reconnect fehlgeschlagen"));
            return;
        }

        _sessions.BindConnection(Context.ConnectionId, session.Code);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(session.Code));
        await Clients.Group(RoomGroup(session.Code)).SendAsync("LobbyState", ToLobbyState(session));
        await SendInventoryUpdateToCaller(session, nickname.Trim());
        await SyncCallerToCurrentPhase(session);
    }

    /// <summary>Re-attach the host after SignalR reconnect.</summary>
    public async Task RejoinHost(string code)
    {
        if (!_sessions.TryGet(code, out var session) || session is null)
        {
            await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage("Raum nicht gefunden"));
            return;
        }

        session.TryRebindHost(Context.ConnectionId);
        _sessions.BindConnection(Context.ConnectionId, session.Code);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(session.Code));
        await Clients.Caller.SendAsync("RoomCreated", new RoomCreatedMessage(session.Code));
        await Clients.Group(RoomGroup(session.Code)).SendAsync("LobbyState", ToLobbyState(session));
        await SyncCallerToCurrentPhase(session);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_sessions.TryGetByConnection(Context.ConnectionId, out var session) && session is not null)
        {
            var isHost = session.IsHost(Context.ConnectionId);
            var wasPlayer = session.MarkDisconnected(Context.ConnectionId);
            _sessions.UnbindConnection(Context.ConnectionId);

            if (isHost)
            {
                CancelAutoAdvance(session.Code, broadcastIfActive: true);
            }

            if (wasPlayer)
            {
                await Clients.Group(RoomGroup(session.Code)).SendAsync("LobbyState", ToLobbyState(session));
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Phase 0 single-question demo (ignores full quiz list).</summary>
    public async Task StartDemo()
    {
        if (!TryGetHostSession(out var session) || session is null)
        {
            return;
        }

        session.SetQuestions([SeedData.DemoQuestion]);
        await StartCurrentNextQuestion(session);
    }

    /// <summary>Start selected quiz from the first question.</summary>
    public async Task StartGame()
    {
        if (!TryGetHostSession(out var session) || session is null)
        {
            return;
        }

        if (session.QuestionCount == 0)
        {
            await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage("Quiz has no questions"));
            return;
        }

        await StartCurrentNextQuestion(session);
    }

    public async Task SubmitAnswer(int optionIndex)
    {
        if (!_sessions.TryGetByConnection(Context.ConnectionId, out var session) || session is null)
        {
            await Clients.Caller.SendAsync("AnswerAccepted", new AnswerAcceptedMessage(false, "Not in a room", 0));
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var (ok, error, points) = session.SubmitAnswer(Context.ConnectionId, optionIndex, now);
        await Clients.Caller.SendAsync("AnswerAccepted", new AnswerAcceptedMessage(ok, error, points));

        if (!ok)
        {
            return;
        }

        var player = session.Players.First(p => p.ConnectionId == Context.ConnectionId);
        await Clients.Group(RoomGroup(session.Code)).SendAsync(
            "ScoreUpdate",
            new ScoreUpdateMessage(player.Nickname, player.Score, points));

        if (session.ShouldEndQuestion(now))
        {
            await BroadcastQuestionEndedAsync(session);
        }
    }

    /// <summary>
    /// Use a player power-up. Always pass <paramref name="targetNickname"/> (null when unused):
    /// SignalR does not support optional parameters or hub overloads.
    /// </summary>
    public async Task UsePowerUp(string powerUpId, string? targetNickname)
    {
        if (!_sessions.TryGetByConnection(Context.ConnectionId, out var session) || session is null)
        {
            await Clients.Caller.SendAsync("PowerUpError", new PowerUpErrorMessage("Not in a room"));
            return;
        }

        if (!PowerUpIdParser.TryParse(powerUpId, out var id))
        {
            await Clients.Caller.SendAsync("PowerUpError", new PowerUpErrorMessage("Unknown power-up"));
            return;
        }

        var (ok, error, result) = session.TryUsePowerUp(Context.ConnectionId, id, targetNickname);
        if (!ok || result is null)
        {
            await Clients.Caller.SendAsync("PowerUpError", new PowerUpErrorMessage(error ?? "Power-up failed"));
            return;
        }

        var wire = PowerUpIdParser.ToWire(result.Id);
        var message = new PowerUpUsedMessage(
            wire,
            true,
            result.MaskedWrongIndexes,
            result.NewEndsAtUtc,
            result.ActorNickname,
            result.TargetNickname,
            result.BlockedByShield,
            result.FxKind);
        await Clients.Caller.SendAsync("PowerUpUsed", message);

        var player = session.Players.First(p => p.ConnectionId == Context.ConnectionId);
        await SendInventoryUpdateToCaller(session, player.Nickname);

        if (result.Id == PowerUpId.Shield)
        {
            await Clients.Group(RoomGroup(session.Code)).SendAsync(
                "PowerUpFx",
                new PowerUpFxMessage("shield_up", wire, result.ActorNickname, null));
        }
        else if (result.Id == PowerUpId.Disrupt)
        {
            await Clients.Group(RoomGroup(session.Code)).SendAsync(
                "PowerUpFx",
                new PowerUpFxMessage("attack_launch", wire, result.ActorNickname, result.TargetNickname));

            if (result.BlockedByShield)
            {
                await Clients.Group(RoomGroup(session.Code)).SendAsync(
                    "PowerUpFx",
                    new PowerUpFxMessage("shield_break", "shield", result.TargetNickname, result.ActorNickname));
                await Clients.Group(RoomGroup(session.Code)).SendAsync(
                    "PowerUpFx",
                    new PowerUpFxMessage("attack_blocked", wire, result.ActorNickname, result.TargetNickname));
            }
            else
            {
                await Clients.Group(RoomGroup(session.Code)).SendAsync(
                    "PowerUpFx",
                    new PowerUpFxMessage("attack_hit", wire, result.ActorNickname, result.TargetNickname));
            }

            // Others (host + opponents) see the attack outcome; caller already got PowerUpUsed.
            await Clients.OthersInGroup(RoomGroup(session.Code)).SendAsync("PowerUpUsed", message);
        }

        if (result.NewEndsAtUtc is not null)
        {
            await Clients.OthersInGroup(RoomGroup(session.Code)).SendAsync("PowerUpUsed", message);
            RescheduleQuestionEnd(session.Code, result.NewEndsAtUtc.Value);
        }
    }

    public async Task HostArenaEvent(string powerUpId)
    {
        if (!_sessions.TryGetByConnection(Context.ConnectionId, out var session) || session is null)
        {
            await Clients.Caller.SendAsync("PowerUpError", new PowerUpErrorMessage("Not in a room"));
            return;
        }

        if (!session.IsHost(Context.ConnectionId))
        {
            await Clients.Caller.SendAsync("PowerUpError", new PowerUpErrorMessage("Only the host can trigger arena events"));
            return;
        }

        if (!PowerUpIdParser.TryParse(powerUpId, out var id))
        {
            await Clients.Caller.SendAsync("PowerUpError", new PowerUpErrorMessage("Unknown power-up"));
            return;
        }

        var (ok, error, newEndsAt) = session.TryHostArenaEvent(Context.ConnectionId, id);
        if (!ok)
        {
            await Clients.Caller.SendAsync("PowerUpError", new PowerUpErrorMessage(error ?? "Arena event failed"));
            return;
        }

        var wire = PowerUpIdParser.ToWire(id);
        await Clients.Group(RoomGroup(session.Code)).SendAsync(
            "ArenaEvent",
            new ArenaEventMessage(wire, newEndsAt));

        if (newEndsAt is not null)
        {
            RescheduleQuestionEnd(session.Code, newEndsAt.Value);
        }
    }

    public async Task NextQuestion()
    {
        if (!TryGetHostSession(out var session) || session is null)
        {
            return;
        }

        CancelAutoAdvance(session.Code, broadcastIfActive: true);

        if (session.Phase == GamePhase.Reveal)
        {
            session.ShowLeaderboard();
            await Clients.Group(RoomGroup(session.Code)).SendAsync(
                "Leaderboard",
                ToLeaderboard(session));
        }

        if (session.Phase != GamePhase.Leaderboard)
        {
            return;
        }

        await AdvanceFromLeaderboardAsync(session, notifyHostOnError: true, viaHubContext: false);
    }

    private async Task StartCurrentNextQuestion(
        GameSession session,
        bool notifyHostOnError = true,
        bool viaHubContext = false)
    {
        CancelAutoAdvance(session.Code, broadcastIfActive: false);

        var next = session.PeekNextQuestion();
        if (next is null)
        {
            if (notifyHostOnError)
            {
                await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage("No question available"));
            }

            return;
        }

        var now = DateTimeOffset.UtcNow;
        var (ok, error) = session.StartQuestion(next, now);
        if (!ok)
        {
            if (notifyHostOnError)
            {
                await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage(error ?? "Cannot start"));
            }

            return;
        }

        if (viaHubContext)
        {
            await BroadcastQuestionStartedViaHubContext(session);
        }
        else
        {
            await BroadcastQuestionStarted(session);
        }

        RescheduleQuestionEnd(session.Code, session.QuestionEndsAtUtc!.Value);
    }

    private bool TryGetHostSession(out GameSession? session)
    {
        if (!_sessions.TryGetByConnection(Context.ConnectionId, out session) || session is null)
        {
            return false;
        }

        if (!session.IsHost(Context.ConnectionId))
        {
            return false;
        }

        return true;
    }

    private async Task BroadcastQuestionStarted(GameSession session)
    {
        var q = session.CurrentQuestion!;
        await Clients.Group(RoomGroup(session.Code)).SendAsync(
            "QuestionStarted",
            ToQuestionStartedMessage(session, q));
    }

    private async Task BroadcastQuestionStartedViaHubContext(GameSession session)
    {
        var q = session.CurrentQuestion!;
        await _hubContext.Clients.Group(RoomGroup(session.Code)).SendAsync(
            "QuestionStarted",
            ToQuestionStartedMessage(session, q));
    }

    private static QuestionStartedMessage ToQuestionStartedMessage(GameSession session, DemoQuestion q) =>
        new(
            session.QuestionIndex,
            session.QuestionCount,
            q.Text,
            q.Options,
            session.QuestionStartedAtUtc!.Value,
            session.QuestionEndsAtUtc!.Value,
            q.ImageUrl);

    private async Task BroadcastQuestionEndedAsync(GameSession session)
    {
        if (!session.ForceEndQuestion())
        {
            return;
        }

        await Clients.Group(RoomGroup(session.Code)).SendAsync(
            "QuestionEnded",
            new QuestionEndedMessage(
                session.CorrectIndex!.Value,
                session.QuestionIndex,
                session.QuestionCount));

        session.ApplyStreakRewards();
        await BroadcastInventoryUpdatesAsync(session);

        session.ShowLeaderboard();
        await Clients.Group(RoomGroup(session.Code)).SendAsync(
            "Leaderboard",
            ToLeaderboard(session));

        CancelQuestionEndTimer(session.Code);
        ScheduleAutoAdvance(session);
    }

    /// <summary>
    /// Replaces any prior question-end delay for this room. Concurrent extensions can race briefly
    /// if two timers fire close together; phase checks keep only one transition to reveal.
    /// </summary>
    private void RescheduleQuestionEnd(string code, DateTimeOffset endsAtUtc)
    {
        CancelQuestionEndTimer(code);

        var delay = endsAtUtc - DateTimeOffset.UtcNow;
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        var cts = new CancellationTokenSource();
        QuestionEndTimers[code] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cts.Token);
                if (!_sessions.TryGet(code, out var session) || session is null)
                {
                    return;
                }

                if (session.Phase != GamePhase.QuestionActive)
                {
                    return;
                }

                await BroadcastQuestionEndedViaHubContextAsync(session);
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer end time.
            }
            catch
            {
                // Timer best-effort; end is always server-driven via QuestionEnded.
            }
        });
    }

    private async Task BroadcastQuestionEndedViaHubContextAsync(GameSession session)
    {
        if (!session.ForceEndQuestion())
        {
            return;
        }

        await _hubContext.Clients.Group(RoomGroup(session.Code)).SendAsync(
            "QuestionEnded",
            new QuestionEndedMessage(
                session.CorrectIndex!.Value,
                session.QuestionIndex,
                session.QuestionCount));

        session.ApplyStreakRewards();
        await BroadcastInventoryUpdatesViaHubContextAsync(session);

        session.ShowLeaderboard();
        await _hubContext.Clients.Group(RoomGroup(session.Code)).SendAsync(
            "Leaderboard",
            ToLeaderboard(session));

        CancelQuestionEndTimer(session.Code);
        ScheduleAutoAdvance(session);
    }

    private void ScheduleAutoAdvance(GameSession session)
    {
        if (!session.AutoAdvance.Enabled || session.Phase != GamePhase.Leaderboard)
        {
            return;
        }

        CancelAutoAdvance(session.Code, broadcastIfActive: false);

        var delay = TimeSpan.FromSeconds(session.AutoAdvance.DelaySeconds);
        var advancesAtUtc = DateTimeOffset.UtcNow + delay;
        var cts = new CancellationTokenSource();
        AutoAdvanceTimers[session.Code] = cts;

        _ = _hubContext.Clients.Group(RoomGroup(session.Code)).SendAsync(
            "AutoAdvanceScheduled",
            new AutoAdvanceScheduledMessage(advancesAtUtc));

        var code = session.Code;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cts.Token);
                if (!_sessions.TryGet(code, out var live) || live is null)
                {
                    return;
                }

                if (live.Phase != GamePhase.Leaderboard)
                {
                    return;
                }

                await AdvanceFromLeaderboardAsync(live, notifyHostOnError: false, viaHubContext: true);
            }
            catch (OperationCanceledException)
            {
                // Host Next or rescheduled auto-advance.
            }
            catch
            {
                // Timer best-effort; host can always advance manually.
            }
        });
    }

    private void CancelAutoAdvance(string code, bool broadcastIfActive)
    {
        if (!AutoAdvanceTimers.TryRemove(code, out var existing))
        {
            return;
        }

        existing.Cancel();
        existing.Dispose();

        if (broadcastIfActive)
        {
            _ = _hubContext.Clients.Group(RoomGroup(code)).SendAsync(
                "AutoAdvanceCancelled",
                new AutoAdvanceCancelledMessage());
        }
    }

    private async Task AdvanceFromLeaderboardAsync(
        GameSession session,
        bool notifyHostOnError,
        bool viaHubContext)
    {
        if (session.Phase != GamePhase.Leaderboard)
        {
            return;
        }

        CancelAutoAdvance(session.Code, broadcastIfActive: false);

        if (session.HasMoreQuestions)
        {
            await StartCurrentNextQuestion(session, notifyHostOnError, viaHubContext);
            return;
        }

        var (ok, error) = session.TryFinish();
        if (!ok)
        {
            if (notifyHostOnError)
            {
                await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage(error ?? "Cannot finish"));
            }

            return;
        }

        var finished = new GameFinishedMessage(ToLeaderboard(session).Entries);
        if (viaHubContext)
        {
            await _hubContext.Clients.Group(RoomGroup(session.Code)).SendAsync("GameFinished", finished);
        }
        else
        {
            await Clients.Group(RoomGroup(session.Code)).SendAsync("GameFinished", finished);
        }
    }

    private static void CancelQuestionEndTimer(string code)
    {
        if (QuestionEndTimers.TryRemove(code, out var existing))
        {
            existing.Cancel();
            existing.Dispose();
        }
    }

    private async Task SyncCallerToCurrentPhase(GameSession session)
    {
        switch (session.Phase)
        {
            case GamePhase.QuestionActive when session.CurrentQuestion is not null
                && session.QuestionStartedAtUtc is not null
                && session.QuestionEndsAtUtc is not null:
                await Clients.Caller.SendAsync(
                    "QuestionStarted",
                    new QuestionStartedMessage(
                        session.QuestionIndex,
                        session.QuestionCount,
                        session.CurrentQuestion.Text,
                        session.CurrentQuestion.Options,
                        session.QuestionStartedAtUtc.Value,
                        session.QuestionEndsAtUtc.Value,
                        session.CurrentQuestion.ImageUrl));
                break;
            case GamePhase.Reveal:
            case GamePhase.Leaderboard:
                await Clients.Caller.SendAsync("Leaderboard", ToLeaderboard(session));
                break;
            case GamePhase.Finished:
                await Clients.Caller.SendAsync(
                    "GameFinished",
                    new GameFinishedMessage(ToLeaderboard(session).Entries));
                break;
        }
    }

    private static LobbyStateMessage ToLobbyState(GameSession session)
    {
        var players = session.Players;
        return new LobbyStateMessage(
            players.Select(p => new LobbyPlayerDto(p.Nickname, p.IsConnected)).ToList(),
            session.ConnectedPlayerCount);
    }

    private static LeaderboardMessage ToLeaderboard(GameSession session) =>
        new(
            session.GetLeaderboard()
                .Select(e => new LeaderboardEntryDto(e.Nickname, e.Score))
                .ToList(),
            session.QuestionIndex,
            session.QuestionCount,
            session.HasMoreQuestions);

    private async Task SendInventoryUpdateToCaller(GameSession session, string nickname)
    {
        var state = session.GetPowerUpState(nickname);
        if (state is null)
        {
            return;
        }

        await Clients.Caller.SendAsync("InventoryUpdate", ToInventoryUpdateMessage(state));
    }

    private async Task BroadcastInventoryUpdatesAsync(GameSession session)
    {
        foreach (var player in session.Players.Where(p => p.IsConnected))
        {
            var state = session.GetPowerUpState(player.Nickname);
            if (state is null || string.IsNullOrEmpty(player.ConnectionId))
            {
                continue;
            }

            await Clients.Client(player.ConnectionId).SendAsync(
                "InventoryUpdate",
                ToInventoryUpdateMessage(state));
        }
    }

    private async Task BroadcastInventoryUpdatesViaHubContextAsync(GameSession session)
    {
        foreach (var player in session.Players.Where(p => p.IsConnected))
        {
            var state = session.GetPowerUpState(player.Nickname);
            if (state is null || string.IsNullOrEmpty(player.ConnectionId))
            {
                continue;
            }

            await _hubContext.Clients.Client(player.ConnectionId).SendAsync(
                "InventoryUpdate",
                ToInventoryUpdateMessage(state));
        }
    }

    private static InventoryUpdateMessage ToInventoryUpdateMessage(PlayerPowerUpState state) =>
        new(
            state.Inventory.ToDictionary(
                kv => PowerUpIdParser.ToWire(kv.Key),
                kv => kv.Value),
            state.Streak);

    private static (PowerUpRoomConfig? Config, string? Error) MapPowerUpConfig(PowerUpConfigDto dto)
    {
        var defaults = PowerUpRoomConfig.CreateDefault();
        var starter = new Dictionary<PowerUpId, int>();
        if (dto.Starter is not null)
        {
            foreach (var (wire, count) in dto.Starter)
            {
                if (!PowerUpIdParser.TryParse(wire, out var id))
                {
                    return (null, $"Unknown starter power-up: {wire}");
                }

                starter[id] = count;
            }
        }
        else
        {
            starter = new Dictionary<PowerUpId, int>(defaults.Starter);
        }

        return (new PowerUpRoomConfig
        {
            Enabled = dto.Enabled,
            Starter = starter,
            StreakRewardEvery = dto.StreakRewardEvery,
            MaxStackPerType = dto.MaxStackPerType,
            HostEventsEnabled = dto.HostEventsEnabled,
            MaxHostEventPerQuestion = dto.MaxHostEventPerQuestion,
            StreakRewardPool = defaults.StreakRewardPool,
        }, null);
    }
}
