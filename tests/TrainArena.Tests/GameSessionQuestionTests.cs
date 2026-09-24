using TrainArena.Game;

namespace TrainArena.Tests;

public class GameSessionQuestionTests
{
    private static DemoQuestion Demo() => new()
    {
        Text = "2+2?",
        Options = ["1", "2", "3", "4"],
        CorrectIndex = 3,
        TimeLimitSeconds = 20
    };

    [Fact]
    public void StartQuestion_RequiresPlayer()
    {
        var session = new GameSessionStore(new RoomCodeGenerator()).Create("host");
        var (ok, error) = session.StartQuestion(Demo(), DateTimeOffset.UtcNow);
        Assert.False(ok);
        Assert.Equal("Need at least one player", error);
    }

    [Fact]
    public void SubmitAnswer_AwardsPointsForCorrectFastAnswer()
    {
        var store = new GameSessionStore(new RoomCodeGenerator());
        var session = store.Create("host");
        session.TryJoin("Azubi1", "p1");
        var start = DateTimeOffset.UtcNow;
        Assert.True(session.StartQuestion(Demo(), start).ok);

        var (ok, error, points) = session.SubmitAnswer("p1", optionIndex: 3, start.AddSeconds(1));

        Assert.True(ok);
        Assert.Null(error);
        Assert.True(points > Scoring.BasePoints);
        Assert.Equal(points, session.Players[0].Score);
    }

    [Fact]
    public void SubmitAnswer_LateAfterEnd_Rejected()
    {
        var store = new GameSessionStore(new RoomCodeGenerator());
        var session = store.Create("host");
        session.TryJoin("Azubi1", "p1");
        var start = DateTimeOffset.UtcNow;
        session.StartQuestion(Demo(), start);

        var (ok, error, _) = session.SubmitAnswer("p1", 3, start.AddSeconds(25));

        Assert.False(ok);
        Assert.Equal("Question already ended", error);
    }

    [Fact]
    public void TryEndQuestion_WhenAllAnswered_MovesToReveal()
    {
        var store = new GameSessionStore(new RoomCodeGenerator());
        var session = store.Create("host");
        session.TryJoin("Azubi1", "p1");
        var start = DateTimeOffset.UtcNow;
        session.StartQuestion(Demo(), start);
        session.SubmitAnswer("p1", 3, start.AddSeconds(1));

        Assert.True(session.TryEndQuestion(start.AddSeconds(1)));
        Assert.Equal(GamePhase.Reveal, session.Phase);
    }
}
