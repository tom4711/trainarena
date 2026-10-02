using TrainArena.Game;

namespace TrainArena.Tests;

public class GameSessionTests
{
    [Fact]
    public void Create_AssignsSixCharRoomCode()
    {
        var store = new GameSessionStore(new RoomCodeGenerator());
        var session = store.Create("host-1", Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.Equal(6, session.Code.Length);
        Assert.Matches("^[A-Z0-9]{6}$", session.Code);
        Assert.Equal("host-1", session.HostConnectionId);
        Assert.Empty(session.Players);
    }

    [Fact]
    public void TryJoin_AddsUniqueNickname()
    {
        var store = new GameSessionStore(new RoomCodeGenerator());
        var session = store.Create("host-1", Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var (ok, error) = session.TryJoin("Azubi1", "conn-a");

        Assert.True(ok);
        Assert.Null(error);
        Assert.Single(session.Players);
        Assert.Equal("Azubi1", session.Players[0].Nickname);
        Assert.Equal("conn-a", session.Players[0].ConnectionId);
    }

    [Fact]
    public void TryJoin_RejectsDuplicateNickname_CaseInsensitive()
    {
        var store = new GameSessionStore(new RoomCodeGenerator());
        var session = store.Create("host-1", Guid.Parse("11111111-1111-1111-1111-111111111111"));
        session.TryJoin("Azubi1", "conn-a");

        var (ok, error) = session.TryJoin("azubi1", "conn-b");

        Assert.False(ok);
        Assert.Equal("Nickname already taken", error);
        Assert.Single(session.Players);
    }

    [Fact]
    public void TryJoin_RejectsBlankNickname()
    {
        var store = new GameSessionStore(new RoomCodeGenerator());
        var session = store.Create("host-1", Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var (ok, error) = session.TryJoin("   ", "conn-a");

        Assert.False(ok);
        Assert.Equal("Nickname required", error);
        Assert.Empty(session.Players);
    }

    [Fact]
    public void TryGet_FindsSessionByCode_CaseInsensitive()
    {
        var store = new GameSessionStore(new RoomCodeGenerator());
        var session = store.Create("host-1", Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.True(store.TryGet(session.Code.ToLowerInvariant(), out var found));
        Assert.Same(session, found);
    }

    [Fact]
    public void TryGet_ReturnsFalseForUnknownCode()
    {
        var store = new GameSessionStore(new RoomCodeGenerator());

        Assert.False(store.TryGet("ZZZZZZ", out var found));
        Assert.Null(found);
    }
}
