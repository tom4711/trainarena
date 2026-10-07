using TrainArena.Game.PowerUps;

namespace TrainArena.Tests;

public class PowerUpRoomConfigTests
{
    [Fact]
    public void CreateDefault_HasExpectedStarterAndStreak()
    {
        var c = PowerUpRoomConfig.CreateDefault();
        Assert.True(c.Enabled);
        Assert.Equal(1, c.Starter[PowerUpId.FiftyFifty]);
        Assert.Equal(1, c.Starter[PowerUpId.Double]);
        Assert.Equal(0, c.Starter[PowerUpId.ExtraTime]);
        Assert.Equal(1, c.Starter[PowerUpId.Shield]);
        Assert.Equal(1, c.Starter[PowerUpId.Disrupt]);
        Assert.Equal(2, c.StreakRewardEvery);
        Assert.Equal(3, c.MaxStackPerType);
        Assert.True(c.HostEventsEnabled);
        Assert.Equal(1, c.MaxHostEventPerQuestion);
    }

    [Fact]
    public void Validate_RejectsNegativeStreak()
    {
        var c = PowerUpRoomConfig.CreateDefault();
        c.StreakRewardEvery = -1;
        Assert.NotNull(c.Validate());
    }

    [Fact]
    public void Catalog_MarksPlayerVsHost()
    {
        Assert.Equal(PowerUpKind.Player, PowerUpCatalog.Get(PowerUpId.FiftyFifty).Kind);
        Assert.Equal(PowerUpKind.Host, PowerUpCatalog.Get(PowerUpId.BoostAll).Kind);
        Assert.False(PowerUpCatalog.Get(PowerUpId.BoostAll).IsCompetitiveTargeting);
    }
}
