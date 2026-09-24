namespace TrainArena.Game;

public sealed class PlayerInfo
{
    public required string Nickname { get; init; }
    public required string ConnectionId { get; init; }
}

/// <summary>
/// In-memory state for one live room (Lobby first; question phases in Task 0.3).
/// </summary>
public sealed class GameSession
{
    private readonly object _gate = new();
    private readonly List<PlayerInfo> _players = new();

    public GameSession(string code, string hostConnectionId)
    {
        Code = code;
        HostConnectionId = hostConnectionId;
    }

    public string Code { get; }
    public string HostConnectionId { get; }

    public IReadOnlyList<PlayerInfo> Players
    {
        get
        {
            lock (_gate)
            {
                return _players.ToList();
            }
        }
    }

    public (bool ok, string? error) TryJoin(string nickname, string connectionId)
    {
        var trimmed = nickname?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return (false, "Nickname required");
        }

        lock (_gate)
        {
            if (_players.Any(p => string.Equals(p.Nickname, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return (false, "Nickname already taken");
            }

            _players.Add(new PlayerInfo
            {
                Nickname = trimmed,
                ConnectionId = connectionId
            });
            return (true, null);
        }
    }
}
