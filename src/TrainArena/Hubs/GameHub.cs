using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrainArena.Contracts;
using TrainArena.Data;
using TrainArena.Game;

namespace TrainArena.Hubs;

/// <summary>
/// SignalR hub for live quiz rooms.
/// </summary>
public sealed class GameHub : Hub
{
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

    public async Task CreateRoom(Guid quizId)
    {
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

        var session = _sessions.Create(Context.ConnectionId, quizId);
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
            var wasPlayer = session.MarkDisconnected(Context.ConnectionId);
            _sessions.UnbindConnection(Context.ConnectionId);
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

        if (session.TryEndQuestion(now))
        {
            await BroadcastQuestionEnded(session);
        }
    }

    public async Task NextQuestion()
    {
        if (!TryGetHostSession(out var session) || session is null)
        {
            return;
        }

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

        if (session.HasMoreQuestions)
        {
            await StartCurrentNextQuestion(session);
            return;
        }

        var (ok, error) = session.TryFinish();
        if (!ok)
        {
            await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage(error ?? "Cannot finish"));
            return;
        }

        await Clients.Group(RoomGroup(session.Code)).SendAsync(
            "GameFinished",
            new GameFinishedMessage(ToLeaderboard(session).Entries));
    }

    private async Task StartCurrentNextQuestion(GameSession session)
    {
        var next = session.PeekNextQuestion();
        if (next is null)
        {
            await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage("No question available"));
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var (ok, error) = session.StartQuestion(next, now);
        if (!ok)
        {
            await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage(error ?? "Cannot start"));
            return;
        }

        await BroadcastQuestionStarted(session);
        ScheduleQuestionEnd(session.Code, session.QuestionEndsAtUtc!.Value);
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
            new QuestionStartedMessage(
                session.QuestionIndex,
                session.QuestionCount,
                q.Text,
                q.Options,
                session.QuestionStartedAtUtc!.Value,
                session.QuestionEndsAtUtc!.Value,
                ImageUrl: null));
    }

    private async Task BroadcastQuestionEnded(GameSession session)
    {
        session.ForceEndQuestion();
        await Clients.Group(RoomGroup(session.Code)).SendAsync(
            "QuestionEnded",
            new QuestionEndedMessage(
                session.CorrectIndex!.Value,
                session.QuestionIndex,
                session.QuestionCount));

        session.ShowLeaderboard();
        await Clients.Group(RoomGroup(session.Code)).SendAsync(
            "Leaderboard",
            ToLeaderboard(session));
    }

    private void ScheduleQuestionEnd(string code, DateTimeOffset endsAtUtc)
    {
        var delay = endsAtUtc - DateTimeOffset.UtcNow;
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay);
                if (!_sessions.TryGet(code, out var session) || session is null)
                {
                    return;
                }

                if (session.Phase != GamePhase.QuestionActive)
                {
                    return;
                }

                session.ForceEndQuestion();
                await _hubContext.Clients.Group(RoomGroup(code)).SendAsync(
                    "QuestionEnded",
                    new QuestionEndedMessage(
                        session.CorrectIndex!.Value,
                        session.QuestionIndex,
                        session.QuestionCount));

                session.ShowLeaderboard();
                await _hubContext.Clients.Group(RoomGroup(code)).SendAsync(
                    "Leaderboard",
                    ToLeaderboard(session));
            }
            catch
            {
                // Timer best-effort; end is always server-driven via QuestionEnded.
            }
        });
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
                        ImageUrl: null));
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
}
