using TrainArena.Game;

namespace TrainArena.Tests;

public class AutoAdvanceOptionsTests
{
    [Fact]
    public void CreateDefault_OffWithFiveSeconds()
    {
        var o = AutoAdvanceOptions.CreateDefault();
        Assert.False(o.Enabled);
        Assert.Equal(5, o.DelaySeconds);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void Validate_AcceptsAllowList(int delay)
    {
        var o = new AutoAdvanceOptions { Enabled = true, DelaySeconds = delay };
        Assert.Null(o.Validate());
    }

    [Fact]
    public void Validate_RejectsOtherDelay()
    {
        var o = new AutoAdvanceOptions { Enabled = true, DelaySeconds = 7 };
        Assert.NotNull(o.Validate());
    }
}
