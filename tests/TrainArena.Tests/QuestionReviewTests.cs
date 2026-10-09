using TrainArena.Game;
using TrainArena.Game.PowerUps;

namespace TrainArena.Tests;

public class QuestionReviewTests
{
    private static (GameSession s, DemoQuestion q0, DemoQuestion q1) TwoQuestionRoom()
    {
        var s = new GameSession("ABC123", "host", Guid.NewGuid());
        s.TryJoin("Ada", "p1");
        s.TryJoin("Bob", "p2");
        var q0 = new DemoQuestion { Text = "Q0?", Options = ["a", "b", "c"], CorrectIndex = 0, TimeLimitSeconds = 20 };
        var q1 = new DemoQuestion { Text = "Q1?", Options = ["Wahr", "Falsch"], CorrectIndex = 1, TimeLimitSeconds = 20 };
        s.SetQuestions([q0, q1]);
        return (s, q0, q1);
    }

    [Fact]
    public void Reveal_AppendsReviewStat_WithCountsAndCorrect()
    {
        var (s, q0, _) = TwoQuestionRoom();
        Assert.True(s.StartQuestion(q0, DateTimeOffset.UtcNow).ok);
        s.SubmitAnswer("p1", 0, DateTimeOffset.UtcNow); // correct
        s.SubmitAnswer("p2", 2, DateTimeOffset.UtcNow); // wrong
        Assert.True(s.ForceEndQuestion());

        Assert.Single(s.Review);
        var r = s.Review[0];
        Assert.Equal(0, r.QuestionIndex);
        Assert.Equal("Q0?", r.Text);
        Assert.Equal(["a", "b", "c"], r.Options);
        Assert.Equal(0, r.CorrectIndex);
        Assert.Equal([1, 0, 1], r.Counts);
        Assert.Equal(2, r.AnsweredCount);
        Assert.Equal(2, r.PlayerCount);
        Assert.Equal(1, r.CorrectCount);
    }

    [Fact]
    public void Reveal_ZeroAnswers_StillListed()
    {
        var (s, q0, _) = TwoQuestionRoom();
        Assert.True(s.StartQuestion(q0, DateTimeOffset.UtcNow).ok);
        Assert.True(s.ForceEndQuestion());

        Assert.Single(s.Review);
        Assert.Equal([0, 0, 0], s.Review[0].Counts);
        Assert.Equal(0, s.Review[0].AnsweredCount);
        Assert.Equal(2, s.Review[0].PlayerCount);
        Assert.Equal(0, s.Review[0].CorrectCount);
    }

    [Fact]
    public void Reveal_AnsweredThenDisconnected_PlayerCountNotBelowAnswered()
    {
        var (s, q0, _) = TwoQuestionRoom();
        Assert.True(s.StartQuestion(q0, DateTimeOffset.UtcNow).ok);
        s.SubmitAnswer("p1", 0, DateTimeOffset.UtcNow);
        s.SubmitAnswer("p2", 1, DateTimeOffset.UtcNow);
        s.MarkDisconnected("p2");
        Assert.True(s.ForceEndQuestion());

        Assert.Equal(2, s.Review[0].AnsweredCount);
        Assert.Equal(2, s.Review[0].PlayerCount);
    }

    [Fact]
    public void SetQuestions_ClearsPreviousReview()
    {
        var (s, q0, _) = TwoQuestionRoom();
        Assert.True(s.StartQuestion(q0, DateTimeOffset.UtcNow).ok);
        Assert.True(s.ForceEndQuestion());
        Assert.Single(s.Review);

        s.SetQuestions([q0]);

        Assert.Empty(s.Review);
    }

    [Fact]
    public void Reveal_DisruptedCorrectPick_CountsAsCorrect()
    {
        var cfg = PowerUpRoomConfig.CreateDefault();
        cfg.Starter[PowerUpId.Disrupt] = 1;
        cfg.Starter[PowerUpId.Shield] = 0;
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), cfg);
        s.TryJoin("Ada", "p1");
        s.TryJoin("Bob", "p2");
        var q = new DemoQuestion { Text = "Q?", Options = ["a", "b"], CorrectIndex = 0, TimeLimitSeconds = 20 };
        s.SetQuestions([q]);
        Assert.True(s.StartQuestion(q, DateTimeOffset.UtcNow).ok);
        Assert.True(s.TryUsePowerUp("p1", PowerUpId.Disrupt, "Bob").ok);
        s.SubmitAnswer("p2", 0, DateTimeOffset.UtcNow); // correct option, disrupted
        s.SubmitAnswer("p1", 1, DateTimeOffset.UtcNow);
        Assert.True(s.ForceEndQuestion());

        Assert.Equal(1, s.Review[0].CorrectCount);
        Assert.Equal([1, 1], s.Review[0].Counts);
    }

    [Fact]
    public void TwoReveals_AppendTwoStats()
    {
        var (s, q0, q1) = TwoQuestionRoom();
        Assert.True(s.StartQuestion(q0, DateTimeOffset.UtcNow).ok);
        s.SubmitAnswer("p1", 0, DateTimeOffset.UtcNow);
        Assert.True(s.ForceEndQuestion());
        s.ShowLeaderboard();
        Assert.True(s.StartQuestion(q1, DateTimeOffset.UtcNow.AddMinutes(1)).ok);
        s.SubmitAnswer("p1", 1, DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.True(s.ForceEndQuestion());

        Assert.Equal(2, s.Review.Count);
        Assert.Equal(1, s.Review[1].QuestionIndex);
        Assert.Equal(2, s.Review[1].Options.Length);
    }
}
