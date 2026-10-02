using TrainArena.Game;

namespace TrainArena.Tests;

public class ScoringTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(20);

    [Fact]
    public void WrongAnswer_ScoresZero()
    {
        Assert.Equal(0, Scoring.Score(correct: false, elapsed: TimeSpan.FromSeconds(1), limit: Limit));
    }

    [Fact]
    public void CorrectFast_ScoresHigherThanCorrectSlow()
    {
        var fast = Scoring.Score(true, TimeSpan.FromSeconds(1), Limit);
        var slow = Scoring.Score(true, TimeSpan.FromSeconds(15), Limit);

        Assert.True(fast > slow, $"expected fast ({fast}) > slow ({slow})");
        Assert.True(fast > 0);
        Assert.True(slow > 0);
    }

    [Fact]
    public void CorrectAtDeadline_StillGetsBasePoints()
    {
        var score = Scoring.Score(true, Limit, Limit);
        Assert.Equal(Scoring.BasePoints, score);
    }

    [Fact]
    public void CorrectInstant_GetsBasePlusFullSpeedBonus()
    {
        var score = Scoring.Score(true, TimeSpan.Zero, Limit);
        Assert.Equal(Scoring.BasePoints + Scoring.MaxSpeedBonus, score);
    }
}
