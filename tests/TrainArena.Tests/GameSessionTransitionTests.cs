using TrainArena.Game;

namespace TrainArena.Tests;

public class GameSessionTransitionTests
{
    private static readonly Guid QuizId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static DemoQuestion Q(string text = "Q?", int correct = 0, int seconds = 20) => new()
    {
        Text = text,
        Options = ["A", "B", "C", "D"],
        CorrectIndex = correct,
        TimeLimitSeconds = seconds
    };

    private static GameSession ReadyLobby(params string[] players)
    {
        var session = new GameSessionStore(new RoomCodeGenerator()).Create("host", QuizId);
        session.SetQuestions([Q("One"), Q("Two")]);
        foreach (var (name, i) in players.Select((n, i) => (n, i)))
        {
            Assert.True(session.TryJoin(name, $"p{i}").ok);
        }

        return session;
    }

    [Fact]
    public void SubmitAnswer_InLobby_Rejected()
    {
        var session = ReadyLobby("Azubi1");
        var (ok, error, _) = session.SubmitAnswer("p0", 0, DateTimeOffset.UtcNow);
        Assert.False(ok);
        Assert.Equal("No active question", error);
        Assert.Equal(GamePhase.Lobby, session.Phase);
    }

    [Fact]
    public void StartQuestion_DuringQuestionActive_Rejected()
    {
        var session = ReadyLobby("Azubi1");
        var now = DateTimeOffset.UtcNow;
        Assert.True(session.StartQuestion(Q(), now).ok);

        var (ok, error) = session.StartQuestion(Q("Two"), now);
        Assert.False(ok);
        Assert.Equal("Cannot start question in current phase", error);
        Assert.Equal(GamePhase.QuestionActive, session.Phase);
    }

    [Fact]
    public void ShowLeaderboard_FromQuestionActive_NoOp()
    {
        var session = ReadyLobby("Azubi1");
        session.StartQuestion(Q(), DateTimeOffset.UtcNow);
        session.ShowLeaderboard();
        Assert.Equal(GamePhase.QuestionActive, session.Phase);
    }

    [Fact]
    public void FullFlow_Reveal_Board_HostNext_AdvancesThenFinishes()
    {
        var session = ReadyLobby("Azubi1");
        session.SetQuestions([Q("One", seconds: 30), Q("Two", seconds: 30)]);
        var t0 = DateTimeOffset.UtcNow;

        Assert.True(session.StartQuestion(session.PeekNextQuestion()!, t0).ok);
        Assert.Equal(0, session.QuestionIndex);
        Assert.True(session.SubmitAnswer("p0", 0, t0.AddSeconds(1)).ok);
        Assert.True(session.TryEndQuestion(t0.AddSeconds(1)));
        Assert.Equal(GamePhase.Reveal, session.Phase);

        session.ShowLeaderboard();
        Assert.Equal(GamePhase.Leaderboard, session.Phase);
        Assert.True(session.HasMoreQuestions);

        var t1 = t0.AddSeconds(5);
        Assert.True(session.StartQuestion(session.PeekNextQuestion()!, t1).ok);
        Assert.Equal(1, session.QuestionIndex);
        Assert.True(session.SubmitAnswer("p0", 0, t1.AddSeconds(1)).ok);
        Assert.True(session.TryEndQuestion(t1.AddSeconds(1)));
        session.ShowLeaderboard();
        Assert.False(session.HasMoreQuestions);

        Assert.True(session.TryFinish().ok);
        Assert.Equal(GamePhase.Finished, session.Phase);
    }

    [Fact]
    public void TryFinish_WhileMoreQuestions_Rejected()
    {
        var session = ReadyLobby("Azubi1");
        var t0 = DateTimeOffset.UtcNow;
        session.StartQuestion(session.PeekNextQuestion()!, t0);
        session.ForceEndQuestion();
        session.ShowLeaderboard();

        var (ok, error) = session.TryFinish();
        Assert.False(ok);
        Assert.Equal("More questions remaining", error);
    }

    [Fact]
    public void SubmitAnswer_AfterForceEnd_Rejected()
    {
        var session = ReadyLobby("Azubi1");
        var t0 = DateTimeOffset.UtcNow;
        session.StartQuestion(Q(seconds: 60), t0);
        session.ForceEndQuestion();

        var (ok, error, _) = session.SubmitAnswer("p0", 0, t0.AddSeconds(1));
        Assert.False(ok);
        Assert.Equal("No active question", error);
    }
}
