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
    private readonly ConcurrentDictionary<string, string> _connectionToCode =
        new(StringComparer.Ordinal);

    public GameSessionStore(RoomCodeGenerator codes)
    {
        _codes = codes;
    }

    public GameSession Create(string hostConnectionId, Guid quizId)
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var code = _codes.Next();
            var session = new GameSession(code, hostConnectionId, quizId);
            if (_sessions.TryAdd(code, session))
            {
                _connectionToCode[hostConnectionId] = code;
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

    public bool TryGetByConnection(string connectionId, out GameSession? session)
    {
        session = null;
        if (!_connectionToCode.TryGetValue(connectionId, out var code))
        {
            return false;
        }

        return TryGet(code, out session);
    }

    public void BindConnection(string connectionId, string code) =>
        _connectionToCode[connectionId] = code;

    public void UnbindConnection(string connectionId) =>
        _connectionToCode.TryRemove(connectionId, out _);
}
