using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainArena.Contracts;
using TrainArena.Data;
using TrainArena.Data.Entities;
using TrainArena.Game;
using TrainArena.Game.PowerUps;

namespace TrainArena.Tests;

public class ExamModeSessionTests
{
    [Fact]
    public void ExamMode_DisablesPowerUps_AndScoresIhkWeights()
    {
        var cfg = PowerUpRoomConfig.CreateDefault();
        cfg.Enabled = true;
        var s = new GameSession("EXAM01", "host", Guid.NewGuid(), cfg, examMode: true);
        Assert.False(s.PowerUpConfig.Enabled);
        Assert.True(s.ExamMode);

        s.TryJoin("Ada", "p1");
        var q0 = new QuizQuestion
        {
            Text = "Q0",
            Options = ["a", "b"],
            CorrectIndex = 0,
            TimeLimitSeconds = 20,
            Points = 1,
            Explanation = "hidden mid-exam",
            Tags = ["sql"],
        };
        var q1 = new QuizQuestion
        {
            Text = "Q1",
            Options = ["a", "b"],
            CorrectIndex = 1,
            TimeLimitSeconds = 20,
            Points = 3,
        };
        s.SetQuestions([q0, q1]);
        Assert.Equal(25, s.GetIhkPointsForQuestion(0));
        Assert.Equal(75, s.GetIhkPointsForQuestion(1));

        Assert.True(s.StartQuestion(q0, DateTimeOffset.UtcNow).ok);
        var (ok, _, points) = s.SubmitAnswer("p1", 0, DateTimeOffset.UtcNow);
        Assert.True(ok);
        Assert.Equal(25, points);
        Assert.True(s.ForceEndQuestion());
        Assert.Equal("hidden mid-exam", s.Review[0].Explanation);
        Assert.Equal(["sql"], s.Review[0].Tags);
        s.ShowLeaderboard();

        Assert.True(s.StartQuestion(q1, DateTimeOffset.UtcNow).ok);
        s.SubmitAnswer("p1", 0, DateTimeOffset.UtcNow); // wrong
        Assert.True(s.ForceEndQuestion());

        var ihk = s.GetIhkResults();
        Assert.Single(ihk);
        Assert.Equal(25, ihk[0].Points);
        Assert.Equal(6, ihk[0].Grade);
        Assert.False(ihk[0].Passed);
    }

    [Fact]
    public void ExamMode_NoSpeedBonus()
    {
        var s = new GameSession("EXAM02", "host", Guid.NewGuid(), examMode: true);
        s.TryJoin("Bob", "p1");
        var q = new QuizQuestion
        {
            Text = "Q",
            Options = ["a", "b"],
            CorrectIndex = 0,
            TimeLimitSeconds = 20,
            Points = 0,
        };
        s.SetQuestions([q]);
        var start = DateTimeOffset.UtcNow;
        Assert.True(s.StartQuestion(q, start).ok);
        var late = start.AddSeconds(18);
        var (_, _, points) = s.SubmitAnswer("p1", 0, late);
        Assert.Equal(100, points);
    }
}

public class ExamModeHubTests : IClassFixture<TrainArenaWebAppFactory>
{
    private readonly TrainArenaWebAppFactory _factory;

    public ExamModeHubTests(TrainArenaWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task ExamMode_GameFinished_IncludesIhkResults_NoMidExplanation()
    {
        var quizId = await SeedExamQuizAsync();

        await using var host = await ConnectAsync();
        await using var player = await ConnectAsync();

        var roomCreated = WaitFor<RoomCreatedMessage>(host, "RoomCreated");
        await host.InvokeAsync(
            "CreateRoom",
            quizId,
            null,
            null,
            new ExamConfigDto(true, ["sql"]),
            null);
        var room = await roomCreated;

        var joined = WaitFor<PlayerJoinedMessage>(host, "PlayerJoined");
        await player.InvokeAsync("JoinRoom", room.Code, "Prüfling");
        await joined;

        var q1 = WaitFor<QuestionStartedMessage>(player, "QuestionStarted");
        var a1 = WaitFor<AnswerAcceptedMessage>(player, "AnswerAccepted");
        var end1 = WaitFor<QuestionEndedMessage>(player, "QuestionEnded");
        var board1 = WaitFor<LeaderboardMessage>(player, "Leaderboard");

        await host.InvokeAsync("StartGame");
        var first = await q1;
        Assert.Equal(0, first.Index);
        Assert.Equal(1, first.TotalQuestions); // tag filter kept only sql question

        await player.InvokeAsync("SubmitAnswer", 0, null);
        Assert.True((await a1).Ok);
        var ended = await end1;
        Assert.Null(ended.Explanation); // exam: no mid-round explanation
        await board1;

        var finished = WaitFor<GameFinishedMessage>(player, "GameFinished");
        await host.InvokeAsync("NextQuestion");
        var done = await finished;

        Assert.True(done.ExamMode);
        Assert.NotNull(done.IhkResults);
        Assert.Single(done.IhkResults!);
        Assert.Equal(100, done.IhkResults![0].Points);
        Assert.Equal(1, done.IhkResults[0].Grade);
        Assert.True(done.IhkResults[0].Passed);
        Assert.Single(done.Review);
        Assert.Equal("SQL erklärt", done.Review[0].Explanation);
        Assert.Contains("sql", done.Review[0].Tags!);
    }

    [Fact]
    public async Task TagsApi_ReturnsDistinctNormalizedTags()
    {
        var quizId = await SeedExamQuizAsync();
        var client = _factory.CreateClient();
        var res = await client.GetAsync($"/api/quizzes/{quizId}/tags");
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var tags = doc.RootElement.GetProperty("tags").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("sql", tags);
        Assert.Contains("netzwerk", tags);
    }

    private async Task<Guid> SeedExamQuizAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.NewGuid();
        db.Quizzes.Add(new Quiz
        {
            Id = id,
            Title = "Exam Tags",
            Questions =
            [
                new Question
                {
                    Id = Guid.NewGuid(),
                    QuizId = id,
                    Text = "SQL?",
                    Option0 = "Ja",
                    Option1 = "Nein",
                    CorrectIndex = 0,
                    TimeLimitSeconds = 30,
                    SortOrder = 0,
                    Tags = "sql",
                    Explanation = "SQL erklärt",
                    Points = 2,
                },
                new Question
                {
                    Id = Guid.NewGuid(),
                    QuizId = id,
                    Text = "Netz?",
                    Option0 = "Ja",
                    Option1 = "Nein",
                    CorrectIndex = 0,
                    TimeLimitSeconds = 30,
                    SortOrder = 1,
                    Tags = "netzwerk",
                    Explanation = "Netz erklärt",
                    Points = 2,
                },
            ],
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<HubConnection> ConnectAsync()
    {
        var server = _factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(server.BaseAddress!, "/hubs/game"),
                options => options.HttpMessageHandlerFactory = _ => server.CreateHandler())
            .Build();
        await connection.StartAsync();
        return connection;
    }

    private static Task<T> WaitFor<T>(HubConnection connection, string methodName)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>(methodName, payload =>
        {
            var value = payload.Deserialize<T>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (value is not null)
            {
                tcs.TrySetResult(value);
            }
        });
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(8));
    }
}
