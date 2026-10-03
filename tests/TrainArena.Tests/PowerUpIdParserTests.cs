using TrainArena.Game.PowerUps;

namespace TrainArena.Tests;

public class PowerUpIdParserTests
{
    [Theory]
    [InlineData("fifty_fifty", PowerUpId.FiftyFifty)]
    [InlineData("double", PowerUpId.Double)]
    [InlineData("extra_time", PowerUpId.ExtraTime)]
    [InlineData("shield", PowerUpId.Shield)]
    [InlineData("boost_all", PowerUpId.BoostAll)]
    [InlineData("time_plus", PowerUpId.TimePlus)]
    public void TryParse_KnownWireIds_ReturnsTrue(string wire, PowerUpId expected)
    {
        Assert.True(PowerUpIdParser.TryParse(wire, out var id));
        Assert.Equal(expected, id);
        Assert.Equal(wire, PowerUpIdParser.ToWire(id));
    }

    [Fact]
    public void TryParse_Unknown_ReturnsFalse()
    {
        Assert.False(PowerUpIdParser.TryParse("nope", out _));
    }
}
