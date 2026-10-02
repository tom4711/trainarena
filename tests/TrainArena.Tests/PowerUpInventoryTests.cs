using TrainArena.Game;
using TrainArena.Game.PowerUps;

namespace TrainArena.Tests;

public class PowerUpInventoryTests
{
    [Fact]
    public void TryJoin_CopiesStarterInventory()
    {
        var session = new GameSession("ABC123", "host1", Guid.NewGuid(), PowerUpRoomConfig.CreateDefault());
        Assert.True(session.TryJoin("Ada", "c1").ok);
        var state = session.GetPowerUpState("Ada");
        Assert.NotNull(state);
        Assert.Equal(1, state!.Inventory[PowerUpId.FiftyFifty]);
        Assert.Equal(1, state.Inventory[PowerUpId.Double]);
        Assert.Equal(0, state.Inventory[PowerUpId.ExtraTime]);
        Assert.Equal(1, state.Inventory[PowerUpId.Shield]);
        Assert.Equal(0, state.Streak);
    }

    [Fact]
    public void TryJoin_WhenDisabled_StillJoinsWithEmptyInventory()
    {
        var cfg = PowerUpRoomConfig.CreateDefault();
        cfg.Enabled = false;
        var session = new GameSession("ABC123", "host1", Guid.NewGuid(), cfg);
        Assert.True(session.TryJoin("Ada", "c1").ok);
        var state = session.GetPowerUpState("Ada")!;
        Assert.All(state.Inventory.Values, v => Assert.Equal(0, v));
    }
}
