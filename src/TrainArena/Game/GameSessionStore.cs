using System.Collections.Concurrent;

namespace TrainArena.Game;

/// <summary>
/// Process-wide registry of active in-memory game sessions.
/// </summary>
public sealed class GameSessionStore
{
    private readonly RoomCodeGenerator _codes;
    private readonly ConcurrentDictionary<string, GameSession> _sessions =
        new(StringComparer.OrdinalIgnoreCase);

    public GameSessionStore(RoomCodeGenerator codes)
    {
        _codes = codes;
    }

    public GameSession Create(string hostConnectionId)
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var code = _codes.Next();
            var session = new GameSession(code, hostConnectionId);
            if (_sessions.TryAdd(code, session))
            {
                return session;
            }
        }

        throw new InvalidOperationException("Could not allocate a unique room code.");
    }

    public bool TryGet(string code, out GameSession? session)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            session = null;
            return false;
        }

        return _sessions.TryGetValue(code.Trim(), out session);
    }
}
