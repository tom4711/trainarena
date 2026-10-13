using TrainArena.Game;

namespace TrainArena.Tests;

public class AsyncRaceSessionTests
{
    [Fact]
    public void Constructor_SetsAsyncRaceEnabled()
    {
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), asyncRaceEnabled: true);
        Assert.True(s.AsyncRaceEnabled);
        Assert.Equal(GamePhase.Lobby, s.Phase);
    }
}
