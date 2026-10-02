using TrainArena.Game;

namespace TrainArena.Tests;

public class ReconnectTests
{
    private static readonly Guid QuizId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void MarkDisconnected_ThenTryJoin_SameNickname_Reattaches()
    {
        var store = new GameSessionStore(new RoomCodeGenerator());
        var session = store.Create("host", QuizId);
        Assert.True(session.TryJoin("Azubi1", "conn-1").ok);
        Assert.True(session.MarkDisconnected("conn-1"));
        Assert.Equal(0, session.ConnectedPlayerCount);

        var (ok, error) = session.TryJoin("Azubi1", "conn-2");
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(1, session.ConnectedPlayerCount);
        Assert.Equal("conn-2", session.Players[0].ConnectionId);
    }

    [Fact]
    public void TryRejoin_UnknownNickname_Fails()
    {
        var session = new GameSessionStore(new RoomCodeGenerator()).Create("host", QuizId);
        var (ok, error) = session.TryRejoin("Ghost", "c1");
        Assert.False(ok);
        Assert.Equal("Unknown nickname for this room", error);
    }

    [Fact]
    public void TryRebindHost_UpdatesHostConnection()
    {
        var session = new GameSessionStore(new RoomCodeGenerator()).Create("host-old", QuizId);
        Assert.True(session.TryRebindHost("host-new"));
        Assert.True(session.IsHost("host-new"));
        Assert.False(session.IsHost("host-old"));
    }
}
