using TrainArena.Game;
using TrainArena.Game.PowerUps;

namespace TrainArena.Tests;

public class PowerUpUseTests
{
    private static (GameSession s, DemoQuestion q) ActiveRoom(PowerUpRoomConfig? config = null)
    {
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), config ?? PowerUpRoomConfig.CreateDefault());
        s.TryJoin("Ada", "p1");
        var q = new DemoQuestion { Text = "Q?", Options = ["a", "b", "c", "d"], CorrectIndex = 0, TimeLimitSeconds = 20 };
        s.SetQuestions([q]);
        Assert.True(s.StartQuestion(q, DateTimeOffset.UtcNow).ok);
        return (s, q);
    }

    [Fact]
    public void FiftyFifty_MasksTwoWrongOptions()
    {
        var (s, _) = ActiveRoom();
        var (ok, err, result) = s.TryUsePowerUp("p1", PowerUpId.FiftyFifty);
        Assert.True(ok, err);
        Assert.NotNull(result!.MaskedWrongIndexes);
        Assert.Equal(2, result.MaskedWrongIndexes!.Length);
        Assert.DoesNotContain(0, result.MaskedWrongIndexes);
        Assert.Equal(PowerUpId.FiftyFifty, result.Id);
        Assert.Equal(0, s.GetPowerUpState("Ada")!.Inventory[PowerUpId.FiftyFifty]);
    }

    [Fact]
    public void Use_RejectsWhenAlreadyAnswered()
    {
        var (s, _) = ActiveRoom();
        s.SubmitAnswer("p1", 0, DateTimeOffset.UtcNow);
        var (ok, err, _) = s.TryUsePowerUp("p1", PowerUpId.Double);
        Assert.False(ok);
        Assert.Contains("answered", err!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Use_RejectsSecondSelfEffect()
    {
        var (s, _) = ActiveRoom();
        Assert.True(s.TryUsePowerUp("p1", PowerUpId.Double).ok);
        var (ok, _, _) = s.TryUsePowerUp("p1", PowerUpId.FiftyFifty);
        Assert.False(ok);
    }

    [Fact]
    public void Shield_AllowedAlongsideSelfEffect()
    {
        var (s, _) = ActiveRoom();
        Assert.True(s.TryUsePowerUp("p1", PowerUpId.Double).ok);
        var (ok, err, _) = s.TryUsePowerUp("p1", PowerUpId.Shield);
        Assert.True(ok, err);
        Assert.True(s.GetPowerUpState("Ada")!.HasShield);
    }

    [Fact]
    public void ExtraTime_ExtendsEndsAtOnce()
    {
        var cfg = PowerUpRoomConfig.CreateDefault();
        cfg.Starter[PowerUpId.ExtraTime] = 1;
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), cfg);
        s.TryJoin("Ada", "p1");
        s.TryJoin("Bob", "p2");
        var q = new DemoQuestion { Text = "Q?", Options = ["a", "b", "c", "d"], CorrectIndex = 0, TimeLimitSeconds = 20 };
        s.SetQuestions([q]);
        Assert.True(s.StartQuestion(q, DateTimeOffset.UtcNow).ok);
        var before = s.QuestionEndsAtUtc!.Value;
        Assert.True(s.TryUsePowerUp("p1", PowerUpId.ExtraTime).ok);
        Assert.Equal(before.AddSeconds(5), s.QuestionEndsAtUtc);
        var afterFirst = s.QuestionEndsAtUtc!.Value;
        Assert.True(s.TryUsePowerUp("p2", PowerUpId.ExtraTime).ok);
        Assert.Equal(afterFirst, s.QuestionEndsAtUtc);
        Assert.Equal(0, s.GetPowerUpState("Ada")!.Inventory[PowerUpId.ExtraTime]);
        Assert.Equal(0, s.GetPowerUpState("Bob")!.Inventory[PowerUpId.ExtraTime]);
    }

    [Fact]
    public void FiftyFifty_OnThreeOptions_MasksOneWrong()
    {
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), PowerUpRoomConfig.CreateDefault());
        s.TryJoin("Ada", "p1");
        var q = new DemoQuestion { Text = "Q?", Options = ["a", "b", "c"], CorrectIndex = 0, TimeLimitSeconds = 20 };
        s.SetQuestions([q]);
        Assert.True(s.StartQuestion(q, DateTimeOffset.UtcNow).ok);
        var (ok, err, result) = s.TryUsePowerUp("p1", PowerUpId.FiftyFifty);
        Assert.True(ok, err);
        Assert.NotNull(result!.MaskedWrongIndexes);
        Assert.Single(result.MaskedWrongIndexes!);
        Assert.DoesNotContain(0, result.MaskedWrongIndexes);
    }

    [Fact]
    public void FiftyFifty_OnTwoOptions_Rejected()
    {
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), PowerUpRoomConfig.CreateDefault());
        s.TryJoin("Ada", "p1");
        var q = new DemoQuestion { Text = "Q?", Options = ["Wahr", "Falsch"], CorrectIndex = 0, TimeLimitSeconds = 20 };
        s.SetQuestions([q]);
        Assert.True(s.StartQuestion(q, DateTimeOffset.UtcNow).ok);
        var (ok, err, _) = s.TryUsePowerUp("p1", PowerUpId.FiftyFifty);
        Assert.False(ok);
        Assert.Contains("50/50", err!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, s.GetPowerUpState("Ada")!.Inventory[PowerUpId.FiftyFifty]);
    }

    [Fact]
    public void StartQuestion_ClearsPerQuestionFlags_KeepsShield()
    {
        var (s, q) = ActiveRoom();
        Assert.True(s.TryUsePowerUp("p1", PowerUpId.Shield).ok);
        Assert.True(s.TryUsePowerUp("p1", PowerUpId.FiftyFifty).ok);
        s.ForceEndQuestion();
        s.ShowLeaderboard();
        Assert.True(s.StartQuestion(q, DateTimeOffset.UtcNow.AddMinutes(1)).ok);
        var state = s.GetPowerUpState("Ada")!;
        Assert.Null(state.MaskedWrongIndexes);
        Assert.False(state.DoubleActive);
        Assert.False(state.UsedExtraTimeThisQuestion);
        Assert.True(state.HasShield);
    }
}
