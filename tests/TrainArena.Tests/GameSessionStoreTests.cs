using TrainArena.Game;

namespace TrainArena.Tests;

public class GameSessionStoreTests
{
    [Fact]
    public void Remove_DropsSessionAndConnectionBindings()
    {
        var store = new GameSessionStore(new RoomCodeGenerator());
        var session = store.Create("host-1", Guid.NewGuid());
        store.BindConnection("player-1", session.Code);
        Assert.Equal(1, store.ActiveSessionCount);
        Assert.True(store.TryGetByConnection("player-1", out _));

        Assert.True(store.Remove(session.Code));

        Assert.Equal(0, store.ActiveSessionCount);
        Assert.False(store.TryGet(session.Code, out _));
        Assert.False(store.TryGetByConnection("host-1", out _));
        Assert.False(store.TryGetByConnection("player-1", out _));
    }

    [Fact]
    public void Remove_UnknownCode_ReturnsFalse()
    {
        var store = new GameSessionStore(new RoomCodeGenerator());
        Assert.False(store.Remove("ZZZZZZ"));
    }
}
