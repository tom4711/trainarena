using TrainArena.Game;
using TrainArena.Game.PowerUps;

namespace TrainArena.Tests;

public class PowerUpScoringTests
{
    private static int TotalInventory(PlayerPowerUpState state) =>
        PowerUpCatalog.PlayerIds.Sum(id => state.Inventory[id]);

    private static (GameSession s, DemoQuestion q, DateTimeOffset t0) ActiveRoom(
        PowerUpRoomConfig? config = null,
        int timeLimitSeconds = 20)
    {
        var t0 = DateTimeOffset.UtcNow;
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), config ?? PowerUpRoomConfig.CreateDefault());
        s.TryJoin("Ada", "p1");
        var q = new DemoQuestion
        {
            Text = "Q?",
            Options = ["a", "b", "c", "d"],
            CorrectIndex = 0,
            TimeLimitSeconds = timeLimitSeconds,
        };
        s.SetQuestions([q]);
        Assert.True(s.StartQuestion(q, t0).ok);
        return (s, q, t0);
    }

    [Fact]
    public void Double_ThenBoostAll_MultipliesInOrder()
    {
        var (s, _, t0) = ActiveRoom();
        Assert.True(s.TryUsePowerUp("p1", PowerUpId.Double).ok);
        Assert.True(s.TryHostArenaEvent("host", PowerUpId.BoostAll).ok);

        var (_, _, points) = s.SubmitAnswer("p1", 0, t0);
        Assert.Equal(3000, points);
        Assert.Equal(3000, s.Players[0].Score);
        Assert.False(s.GetPowerUpState("Ada")!.DoubleActive);
    }

    [Fact]
    public void ForceEndQuestion_ClearsDoubleWhenUnanswered()
    {
        var (s, _, _) = ActiveRoom();
        Assert.True(s.TryUsePowerUp("p1", PowerUpId.Double).ok);
        Assert.True(s.GetPowerUpState("Ada")!.DoubleActive);

        s.ForceEndQuestion();

        Assert.False(s.GetPowerUpState("Ada")!.DoubleActive);
    }

    [Fact]
    public void WrongAnswer_ClearsDoubleWithoutPoints()
    {
        var (s, _, t0) = ActiveRoom();
        Assert.True(s.TryUsePowerUp("p1", PowerUpId.Double).ok);

        var (_, _, points) = s.SubmitAnswer("p1", 1, t0.AddSeconds(1));
        Assert.Equal(0, points);
        Assert.Equal(0, s.Players[0].Score);
        Assert.False(s.GetPowerUpState("Ada")!.DoubleActive);
    }

    [Fact]
    public void StreakEvery2_GrantsFromPool()
    {
        var cfg = PowerUpRoomConfig.CreateDefault();
        cfg.StreakRewardEvery = 2;
        cfg.StreakRewardPool = [PowerUpId.Double];
        var q1 = new DemoQuestion
        {
            Text = "Q?",
            Options = ["a", "b", "c", "d"],
            CorrectIndex = 0,
            TimeLimitSeconds = 20,
        };
        var q2 = new DemoQuestion
        {
            Text = "Q2?",
            Options = ["a", "b", "c", "d"],
            CorrectIndex = 0,
            TimeLimitSeconds = 20,
        };
        var t0 = DateTimeOffset.UtcNow;
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), cfg);
        s.TryJoin("Ada", "p1");
        s.SetQuestions([q1, q2]);

        Assert.True(s.StartQuestion(q1, t0).ok);
        s.SubmitAnswer("p1", 0, t0.AddSeconds(1));
        s.ForceEndQuestion();
        var grants1 = s.ApplyStreakRewards(new Random(0));
        Assert.Empty(grants1);
        Assert.Equal(1, s.GetPowerUpState("Ada")!.Streak);

        s.ShowLeaderboard();
        var t1 = t0.AddMinutes(1);
        Assert.True(s.StartQuestion(q2, t1).ok);
        var before = TotalInventory(s.GetPowerUpState("Ada")!);
        s.SubmitAnswer("p1", 0, t1.AddSeconds(1));
        s.ForceEndQuestion();
        var grants2 = s.ApplyStreakRewards(new Random(0));

        Assert.Single(grants2);
        Assert.Equal("Ada", grants2[0].Nickname);
        Assert.Equal(PowerUpId.Double, grants2[0].PowerUpId);
        Assert.Equal(before + 1, TotalInventory(s.GetPowerUpState("Ada")!));
        Assert.Equal(2, s.GetPowerUpState("Ada")!.Streak);
    }

    [Fact]
    public void HostTimePlus_ExtendsEnds()
    {
        var (s, _, _) = ActiveRoom();
        var before = s.QuestionEndsAtUtc!.Value;
        var (ok1, err1, newEnds) = s.TryHostArenaEvent("host", PowerUpId.TimePlus);
        Assert.True(ok1, err1);
        Assert.Equal(before.AddSeconds(5), newEnds);
        Assert.Equal(before.AddSeconds(5), s.QuestionEndsAtUtc);

        var (ok2, err2, _) = s.TryHostArenaEvent("host", PowerUpId.TimePlus);
        Assert.False(ok2);
        Assert.NotNull(err2);
    }

    [Fact]
    public void HostBoostAll_RejectedWhenDisabled()
    {
        var cfg = PowerUpRoomConfig.CreateDefault();
        cfg.HostEventsEnabled = false;
        var (s, _, _) = ActiveRoom(cfg);
        var (ok, err, _) = s.TryHostArenaEvent("host", PowerUpId.BoostAll);
        Assert.False(ok);
        Assert.NotNull(err);
    }
}
