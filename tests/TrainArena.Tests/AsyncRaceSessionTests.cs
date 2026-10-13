using TrainArena.Game;
using TrainArena.Game.PowerUps;

namespace TrainArena.Tests;

public class AsyncRaceSessionTests
{
    [Fact]
    public void Constructor_SetsAsyncRaceEnabled()
    {
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), asyncRaceEnabled: true);
        Assert.True(s.AsyncRaceEnabled);
        Assert.Equal(GamePhase.Lobby, s.Phase);
    }

    [Fact]
    public void StartAsyncRace_ShufflesPerPlayer_AndSetsPhase()
    {
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), asyncRaceEnabled: true);
        s.SetQuestions(Enumerable.Range(0, 5).Select(i => new DemoQuestion
        {
            Text = $"Q{i}", Options = ["a", "b"], CorrectIndex = 0, TimeLimitSeconds = 20
        }).ToList());
        s.TryJoin("Ada", "c1");
        s.TryJoin("Bob", "c2");

        Assert.True(s.StartAsyncRace(DateTimeOffset.UtcNow).ok);
        Assert.Equal(GamePhase.AsyncRacing, s.Phase);

        var ada = s.GetRaceState("Ada")!;
        var bob = s.GetRaceState("Bob")!;
        Assert.Equal(5, ada.Order.Count);
        Assert.Equal(0, ada.Index);
        Assert.NotNull(ada.Current);
        Assert.Equal(
            ada.Order.Select(q => q.Text).OrderBy(x => x),
            bob.Order.Select(q => q.Text).OrderBy(x => x));
    }

    [Fact]
    public void StartAsyncRace_RejectedWhenNotAsyncMode()
    {
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), asyncRaceEnabled: false);
        s.SetQuestions([new DemoQuestion { Text = "Q", Options = ["a", "b"], CorrectIndex = 0 }]);
        s.TryJoin("Ada", "c1");
        Assert.False(s.StartAsyncRace(DateTimeOffset.UtcNow).ok);
    }

    [Fact]
    public void GetAsyncScoreboard_ShowsZeroProgress()
    {
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), asyncRaceEnabled: true);
        s.SetQuestions([new DemoQuestion { Text = "Q", Options = ["a", "b"], CorrectIndex = 0 }]);
        s.TryJoin("Ada", "c1");
        Assert.True(s.StartAsyncRace(DateTimeOffset.UtcNow).ok);
        var board = s.GetAsyncScoreboard();
        Assert.Single(board);
        Assert.Equal(0, board[0].AnsweredCount);
        Assert.Equal(1, board[0].Total);
    }

    [Fact]
    public void SubmitAsyncAnswer_AdvancesOnlyThatPlayer()
    {
        var s = NewAsyncTwoPlayerTwoQuestions();
        Assert.True(s.StartAsyncRace(DateTimeOffset.UtcNow).ok);
        var adaQ = s.GetRaceState("Ada")!.Current!;
        var r = s.SubmitAsyncAnswer("Ada", adaQ.CorrectIndex, DateTimeOffset.UtcNow);
        Assert.True(r.Ok);
        Assert.Equal(1, s.GetRaceState("Ada")!.Index);
        Assert.Equal(0, s.GetRaceState("Bob")!.Index);
        Assert.False(r.RaceFinished);
    }

    [Fact]
    public void AllPlayersFinished_SetsPhaseFinished()
    {
        var s = NewAsyncOnePlayerOneQuestion();
        Assert.True(s.StartAsyncRace(DateTimeOffset.UtcNow).ok);
        var q = s.GetRaceState("Ada")!.Current!;
        var r = s.SubmitAsyncAnswer("Ada", q.CorrectIndex, DateTimeOffset.UtcNow);
        Assert.True(r.PlayerFinished);
        Assert.True(r.RaceFinished);
        Assert.Equal(GamePhase.Finished, s.Phase);
    }

    [Fact]
    public void Disrupt_RejectsFinishedTarget()
    {
        var cfg = PowerUpRoomConfig.CreateDefault();
        cfg.Starter[PowerUpId.Disrupt] = 1;
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), cfg, asyncRaceEnabled: true);
        s.SetQuestions(
        [
            new DemoQuestion { Text = "Q1", Options = ["a", "b"], CorrectIndex = 0, TimeLimitSeconds = 20 },
            new DemoQuestion { Text = "Q2", Options = ["a", "b"], CorrectIndex = 0, TimeLimitSeconds = 20 },
        ]);
        s.TryJoin("Ada", "c1");
        s.TryJoin("Bob", "c2");
        s.TryJoin("Charlie", "c3");
        var now = DateTimeOffset.UtcNow;
        Assert.True(s.StartAsyncRace(now).ok);

        var adaQ1 = s.GetRaceState("Ada")!.Current!;
        Assert.True(s.SubmitAsyncAnswer("Ada", adaQ1.CorrectIndex, now).Ok);
        var adaQ2 = s.GetRaceState("Ada")!.Current!;
        Assert.True(s.SubmitAsyncAnswer("Ada", adaQ2.CorrectIndex, now).Ok);
        Assert.True(s.GetRaceState("Ada")!.Finished);
        Assert.False(s.GetRaceState("Bob")!.Finished);
        Assert.False(s.GetRaceState("Charlie")!.Finished);

        var (ok, err, _) = s.TryUsePowerUp("c2", PowerUpId.Disrupt, "Ada");
        Assert.False(ok);
        Assert.Contains("Invalid target", err!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FiftyFifty_AllowedOnNextRaceQuestionAfterSubmitAdvance()
    {
        var cfg = PowerUpRoomConfig.CreateDefault();
        cfg.Starter[PowerUpId.FiftyFifty] = 2;
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), cfg, asyncRaceEnabled: true);
        s.SetQuestions(
        [
            new DemoQuestion
            {
                Text = "Q1",
                Options = ["a", "b", "c", "d"],
                CorrectIndex = 0,
                TimeLimitSeconds = 20,
            },
            new DemoQuestion
            {
                Text = "Q2",
                Options = ["a", "b", "c", "d"],
                CorrectIndex = 1,
                TimeLimitSeconds = 20,
            },
        ]);
        s.TryJoin("Ada", "c1");
        var now = DateTimeOffset.UtcNow;
        Assert.True(s.StartAsyncRace(now).ok);

        Assert.True(s.TryUsePowerUp("c1", PowerUpId.FiftyFifty).ok);
        Assert.NotNull(s.GetPowerUpState("Ada")!.MaskedWrongIndexes);

        var q1 = s.GetRaceState("Ada")!.Current!;
        Assert.True(s.SubmitAsyncAnswer("Ada", q1.CorrectIndex, now).Ok);
        Assert.Null(s.GetPowerUpState("Ada")!.MaskedWrongIndexes);

        var (ok, useErr, result) = s.TryUsePowerUp("c1", PowerUpId.FiftyFifty);
        Assert.True(ok, useErr);
        Assert.NotNull(result!.MaskedWrongIndexes);
    }

    [Fact]
    public void ExtraTime_AllowedOnNextRaceQuestionAfterTimeoutAdvance()
    {
        var cfg = PowerUpRoomConfig.CreateDefault();
        cfg.Starter[PowerUpId.ExtraTime] = 2;
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), cfg, asyncRaceEnabled: true);
        s.SetQuestions(
        [
            new DemoQuestion { Text = "Q1", Options = ["a", "b"], CorrectIndex = 0, TimeLimitSeconds = 20 },
            new DemoQuestion { Text = "Q2", Options = ["a", "b"], CorrectIndex = 0, TimeLimitSeconds = 20 },
        ]);
        s.TryJoin("Ada", "c1");
        var start = DateTimeOffset.UtcNow;
        Assert.True(s.StartAsyncRace(start).ok);
        Assert.True(s.TryUsePowerUp("c1", PowerUpId.ExtraTime).ok);
        Assert.True(s.GetPowerUpState("Ada")!.UsedExtraTimeThisQuestion);

        var endsAt = s.GetRaceState("Ada")!.EndsAtUtc!.Value;
        Assert.True(s.TryTimeoutAsyncPlayer("Ada", endsAt).timedOut);
        Assert.False(s.GetPowerUpState("Ada")!.UsedExtraTimeThisQuestion);

        var before = s.GetRaceState("Ada")!.EndsAtUtc!.Value;
        var (ok, err, result) = s.TryUsePowerUp("c1", PowerUpId.ExtraTime);
        Assert.True(ok, err);
        Assert.Equal(before.AddSeconds(5), s.GetRaceState("Ada")!.EndsAtUtc);
        Assert.Equal(before.AddSeconds(5), result!.NewEndsAtUtc);
    }

    [Fact]
    public void ExtraTime_ExtendsAsyncEndsAt()
    {
        var cfg = PowerUpRoomConfig.CreateDefault();
        cfg.Starter[PowerUpId.ExtraTime] = 1;
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), cfg, asyncRaceEnabled: true);
        s.SetQuestions(
        [
            new DemoQuestion
            {
                Text = "Q",
                Options = ["a", "b", "c", "d"],
                CorrectIndex = 0,
                TimeLimitSeconds = 20,
            },
        ]);
        s.TryJoin("Ada", "c1");
        var start = DateTimeOffset.UtcNow;
        Assert.True(s.StartAsyncRace(start).ok);
        var before = s.GetRaceState("Ada")!.EndsAtUtc!.Value;

        var (ok, err, result) = s.TryUsePowerUp("c1", PowerUpId.ExtraTime);
        Assert.True(ok, err);
        Assert.Equal(before.AddSeconds(5), s.GetRaceState("Ada")!.EndsAtUtc);
        Assert.Equal(before.AddSeconds(5), result!.NewEndsAtUtc);
    }

    [Fact]
    public void SubmitAsyncAnswer_RejectsStaleQuestionIndex()
    {
        var s = NewAsyncTwoPlayerTwoQuestions();
        Assert.True(s.StartAsyncRace(DateTimeOffset.UtcNow).ok);
        var adaQ = s.GetRaceState("Ada")!.Current!;
        var now = DateTimeOffset.UtcNow;
        Assert.True(s.SubmitAsyncAnswer("Ada", adaQ.CorrectIndex, now, expectedQuestionIndex: 0).Ok);
        var r = s.SubmitAsyncAnswer("Ada", 0, now, expectedQuestionIndex: 0);
        Assert.False(r.Ok);
        Assert.Contains("advanced", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SubmitAsyncAnswer_AtEndsAtUtc_Rejected()
    {
        var s = NewAsyncOnePlayerOneQuestion();
        Assert.True(s.StartAsyncRace(DateTimeOffset.UtcNow).ok);
        var endsAt = s.GetRaceState("Ada")!.EndsAtUtc!.Value;
        var r = s.SubmitAsyncAnswer("Ada", 0, endsAt);
        Assert.False(r.Ok);
        Assert.Contains("ended", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyStreakRewardForPlayer_GrantsAfterAsyncCorrectStreak()
    {
        var cfg = PowerUpRoomConfig.CreateDefault();
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), cfg, asyncRaceEnabled: true);
        s.SetQuestions(
        [
            new DemoQuestion { Text = "Q1", Options = ["a", "b"], CorrectIndex = 0, TimeLimitSeconds = 20 },
            new DemoQuestion { Text = "Q2", Options = ["a", "b"], CorrectIndex = 0, TimeLimitSeconds = 20 },
        ]);
        s.TryJoin("Ada", "c1");
        Assert.True(s.StartAsyncRace(DateTimeOffset.UtcNow).ok);
        var now = DateTimeOffset.UtcNow;
        var q1 = s.GetRaceState("Ada")!.Current!;
        Assert.True(s.SubmitAsyncAnswer("Ada", q1.CorrectIndex, now).Ok);
        s.ApplyStreakRewardForPlayer("Ada", true, new Random(0));
        Assert.Equal(1, s.GetPowerUpState("Ada")!.Streak);

        var q2 = s.GetRaceState("Ada")!.Current!;
        Assert.True(s.SubmitAsyncAnswer("Ada", q2.CorrectIndex, now).Ok);
        var grants = s.ApplyStreakRewardForPlayer("Ada", true, new Random(0));
        Assert.Single(grants);
        Assert.Equal(2, s.GetPowerUpState("Ada")!.Streak);
    }

    [Fact]
    public void TryTimeoutAsyncPlayer_ClearsDoubleActive()
    {
        var cfg = PowerUpRoomConfig.CreateDefault();
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), cfg, asyncRaceEnabled: true);
        s.SetQuestions(
        [
            new DemoQuestion { Text = "Q1", Options = ["a", "b"], CorrectIndex = 0, TimeLimitSeconds = 20 },
            new DemoQuestion { Text = "Q2", Options = ["a", "b"], CorrectIndex = 0, TimeLimitSeconds = 20 },
        ]);
        s.TryJoin("Ada", "c1");
        var start = DateTimeOffset.UtcNow;
        Assert.True(s.StartAsyncRace(start).ok);
        Assert.True(s.TryUsePowerUp("c1", PowerUpId.Double).ok);
        Assert.True(s.GetPowerUpState("Ada")!.DoubleActive);

        var endsAt = s.GetRaceState("Ada")!.EndsAtUtc!.Value;
        var (timedOut, _) = s.TryTimeoutAsyncPlayer("Ada", endsAt);
        Assert.True(timedOut);
        Assert.False(s.GetPowerUpState("Ada")!.DoubleActive);
    }

    private static GameSession NewAsyncTwoPlayerTwoQuestions()
    {
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), asyncRaceEnabled: true);
        s.SetQuestions(
        [
            new DemoQuestion { Text = "Q1", Options = ["a", "b"], CorrectIndex = 0, TimeLimitSeconds = 20 },
            new DemoQuestion { Text = "Q2", Options = ["a", "b"], CorrectIndex = 1, TimeLimitSeconds = 20 },
        ]);
        s.TryJoin("Ada", "c1");
        s.TryJoin("Bob", "c2");
        return s;
    }

    private static GameSession NewAsyncOnePlayerOneQuestion()
    {
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), asyncRaceEnabled: true);
        s.SetQuestions([new DemoQuestion { Text = "Q", Options = ["a", "b"], CorrectIndex = 0 }]);
        s.TryJoin("Ada", "c1");
        return s;
    }
}
