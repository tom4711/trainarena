using TrainArena.Game;

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
}
