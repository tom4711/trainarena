using Microsoft.AspNetCore.SignalR;
using TrainArena.Contracts;
using TrainArena.Game;

namespace TrainArena.Hubs;

/// <summary>
/// SignalR hub for live quiz rooms (create / join lobby).
/// </summary>
public sealed class GameHub : Hub
{
    private readonly GameSessionStore _sessions;

    public GameHub(GameSessionStore sessions)
    {
        _sessions = sessions;
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

        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(session.Code));

        var players = session.Players;
        await Clients.Group(RoomGroup(session.Code)).SendAsync(
            "PlayerJoined",
            new PlayerJoinedMessage(players[^1].Nickname, players.Count));
        await Clients.Group(RoomGroup(session.Code)).SendAsync("LobbyState", ToLobbyState(session));
    }

    private static LobbyStateMessage ToLobbyState(GameSession session)
    {
        var players = session.Players;
        return new LobbyStateMessage(
            players.Select(p => new LobbyPlayerDto(p.Nickname)).ToList(),
            players.Count);
    }
}
