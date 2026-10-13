using TrainArena.Game;

namespace TrainArena.Tests;

public class IhkGradingTests
{
    [Theory]
    [InlineData(100, 1)]
    [InlineData(92, 1)]
    [InlineData(91, 2)]
    [InlineData(81, 2)]
    [InlineData(80, 3)]
    [InlineData(67, 3)]
    [InlineData(66, 4)]
    [InlineData(50, 4)]
    [InlineData(49, 5)]
    [InlineData(30, 5)]
    [InlineData(29, 6)]
    [InlineData(0, 6)]
    public void GradeFromPoints_MatchesIhkKey(int points, int expectedGrade)
    {
        Assert.Equal(expectedGrade, IhkGrading.GradeFromPoints(points));
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(49, false)]
    public void IsPassed_Threshold50(int points, bool passed)
    {
        Assert.Equal(passed, IhkGrading.IsPassed(points));
    }

    [Fact]
    public void NormalizeToHundred_EqualWeights_SumTo100()
    {
        var result = IhkGrading.NormalizeToHundred([0, 0, 0, 0]);
        Assert.Equal(4, result.Length);
        Assert.Equal(100, result.Sum());
        Assert.All(result, p => Assert.Equal(25, p));
    }

    [Fact]
    public void NormalizeToHundred_UnevenWeights_SumTo100()
    {
        var result = IhkGrading.NormalizeToHundred([1, 1, 2]);
        Assert.Equal(100, result.Sum());
        Assert.Equal(25, result[0]);
        Assert.Equal(25, result[1]);
        Assert.Equal(50, result[2]);
    }

    [Fact]
    public void NormalizeToHundred_Empty_ReturnsEmpty()
    {
        Assert.Empty(IhkGrading.NormalizeToHundred([]));
    }
}
