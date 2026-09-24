using Microsoft.AspNetCore.SignalR;
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

    public GameHub(GameSessionStore sessions, IHubContext<GameHub> hubContext)
    {
        _sessions = sessions;
        _hubContext = hubContext;
    }

    public static string RoomGroup(string code) => $"room:{code.ToUpperInvariant()}";

    public async Task CreateRoom()
    {
        var session = _sessions.Create(Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(session.Code));
        await Clients.Caller.SendAsync("RoomCreated", new RoomCreatedMessage(session.Code));
        await Clients.Caller.SendAsync("LobbyState", ToLobbyState(session));
    }

    public async Task JoinRoom(string code, string nickname)
    {
        if (!_sessions.TryGet(code, out var session) || session is null)
        {
            await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage("Room not found"));
            return;
        }

        var (ok, error) = session.TryJoin(nickname, Context.ConnectionId);
        if (!ok)
        {
            await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage(error ?? "Join failed"));
            return;
        }

        _sessions.BindConnection(Context.ConnectionId, session.Code);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(session.Code));

        var players = session.Players;
        await Clients.Group(RoomGroup(session.Code)).SendAsync(
            "PlayerJoined",
            new PlayerJoinedMessage(players[^1].Nickname, players.Count));
        await Clients.Group(RoomGroup(session.Code)).SendAsync("LobbyState", ToLobbyState(session));
    }

    public async Task StartDemo()
    {
        if (!TryGetHostSession(out var session) || session is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var (ok, error) = session.StartQuestion(SeedData.DemoQuestion, now);
        if (!ok)
        {
            await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage(error ?? "Cannot start"));
            return;
        }

        await BroadcastQuestionStarted(session);
        ScheduleQuestionEnd(session.Code, session.QuestionEndsAtUtc!.Value);
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

        // After reveal/board (auto), Host Next finishes the single-question skeleton.
        if (session.Phase == GamePhase.Reveal)
        {
            session.ShowLeaderboard();
            await Clients.Group(RoomGroup(session.Code)).SendAsync(
                "Leaderboard",
                ToLeaderboard(session));
        }

        if (session.Phase == GamePhase.Leaderboard)
        {
            session.Finish();
            await Clients.Group(RoomGroup(session.Code)).SendAsync(
                "GameFinished",
                new GameFinishedMessage(ToLeaderboard(session).Entries));
        }
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
            new QuestionEndedMessage(session.CorrectIndex!.Value));

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
                    new QuestionEndedMessage(session.CorrectIndex!.Value));

                session.ShowLeaderboard();
                await _hubContext.Clients.Group(RoomGroup(code)).SendAsync(
                    "Leaderboard",
                    ToLeaderboard(session));
            }
            catch
            {
                // Timer best-effort for skeleton; failures are non-fatal.
            }
        });
    }

    private static LobbyStateMessage ToLobbyState(GameSession session)
    {
        var players = session.Players;
        return new LobbyStateMessage(
            players.Select(p => new LobbyPlayerDto(p.Nickname)).ToList(),
            players.Count);
    }

    private static LeaderboardMessage ToLeaderboard(GameSession session) =>
        new(session.GetLeaderboard()
            .Select(e => new LeaderboardEntryDto(e.Nickname, e.Score))
            .ToList());
}
