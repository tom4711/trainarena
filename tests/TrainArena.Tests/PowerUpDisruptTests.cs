using TrainArena.Game;
using TrainArena.Game.PowerUps;

namespace TrainArena.Tests;

public class PowerUpDisruptTests
{
    private static (GameSession s, DemoQuestion q) TwoPlayerRoom(PowerUpRoomConfig? config = null)
    {
        var cfg = config ?? PowerUpRoomConfig.CreateDefault();
        cfg.Starter[PowerUpId.Disrupt] = 1;
        cfg.Starter[PowerUpId.Shield] = 1;
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), cfg);
        s.TryJoin("Ada", "p1");
        s.TryJoin("Bob", "p2");
        var q = new DemoQuestion
        {
            Text = "Q?",
            Options = ["a", "b", "c", "d"],
            CorrectIndex = 0,
            TimeLimitSeconds = 20,
        };
        s.SetQuestions([q]);
        Assert.True(s.StartQuestion(q, DateTimeOffset.UtcNow).ok);
        return (s, q);
    }

    [Fact]
    public void Disrupt_HitsUnshieldedTarget_ZerosNextAnswerAndStreak()
    {
        var (s, _) = TwoPlayerRoom();
        var (ok, err, result) = s.TryUsePowerUp("p1", PowerUpId.Disrupt, "Bob");
        Assert.True(ok, err);
        Assert.False(result!.BlockedByShield);
        Assert.Equal("attack_hit", result.FxKind);
        Assert.Equal("Bob", result.TargetNickname);
        Assert.True(s.GetPowerUpState("Bob")!.DisruptedThisQuestion);

        var now = DateTimeOffset.UtcNow;
        var (ansOk, _, points) = s.SubmitAnswer("p2", 0, now);
        Assert.True(ansOk);
        Assert.Equal(0, points);
        Assert.False(s.GetPowerUpState("Bob")!.DisruptedThisQuestion);

        // Streak should not grow (counts as incorrect).
        s.ForceEndQuestion();
        s.ApplyStreakRewards(new Random(1));
        Assert.Equal(0, s.GetPowerUpState("Bob")!.Streak);
    }

    [Fact]
    public void Disrupt_BlockedByShield_ConsumesShieldWithoutDisrupt()
    {
        var (s, _) = TwoPlayerRoom();
        Assert.True(s.TryUsePowerUp("p2", PowerUpId.Shield).ok);
        Assert.True(s.GetPowerUpState("Bob")!.HasShield);

        var (ok, err, result) = s.TryUsePowerUp("p1", PowerUpId.Disrupt, "Bob");
        Assert.True(ok, err);
        Assert.True(result!.BlockedByShield);
        Assert.Equal("attack_blocked", result.FxKind);
        Assert.False(s.GetPowerUpState("Bob")!.HasShield);
        Assert.False(s.GetPowerUpState("Bob")!.DisruptedThisQuestion);

        var (ansOk, _, points) = s.SubmitAnswer("p2", 0, DateTimeOffset.UtcNow);
        Assert.True(ansOk);
        Assert.True(points > 0);
    }

    [Fact]
    public void Disrupt_RejectsSelfAndAnsweredTarget()
    {
        var (s, _) = TwoPlayerRoom();
        var self = s.TryUsePowerUp("p1", PowerUpId.Disrupt, "Ada");
        Assert.False(self.ok);

        s.SubmitAnswer("p2", 1, DateTimeOffset.UtcNow);
        var answered = s.TryUsePowerUp("p1", PowerUpId.Disrupt, "Bob");
        Assert.False(answered.ok);
    }

    [Fact]
    public void Disrupt_AllowedAlongsideSelfEffect()
    {
        var (s, _) = TwoPlayerRoom();
        Assert.True(s.TryUsePowerUp("p1", PowerUpId.Double).ok);
        var (ok, err, _) = s.TryUsePowerUp("p1", PowerUpId.Disrupt, "Bob");
        Assert.True(ok, err);
    }

    [Fact]
    public void CreateDefault_IncludesDisruptStarter()
    {
        var c = PowerUpRoomConfig.CreateDefault();
        Assert.Equal(1, c.Starter[PowerUpId.Disrupt]);
        Assert.Contains(PowerUpId.Disrupt, PowerUpCatalog.PlayerIds);
        Assert.True(PowerUpCatalog.Get(PowerUpId.Disrupt).IsCompetitiveTargeting);
    }
}
