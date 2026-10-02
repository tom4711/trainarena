using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainArena.Contracts;
using TrainArena.Data;
using TrainArena.Data.Entities;

namespace TrainArena.Tests;

public class FullRoundTests : IClassFixture<TrainArenaWebAppFactory>
{
    private readonly TrainArenaWebAppFactory _factory;

    public FullRoundTests(TrainArenaWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TwoQuestionQuiz_HostNext_AdvancesThenFinishes()
    {
        var quizId = await SeedTwoQuestionQuizAsync();

        await using var host = await ConnectAsync();
        await using var player = await ConnectAsync();

        var roomCreated = WaitFor<RoomCreatedMessage>(host, "RoomCreated");
        await host.InvokeAsync("CreateRoom", quizId);
        var room = await roomCreated;

        var joined = WaitFor<PlayerJoinedMessage>(host, "PlayerJoined");
        await player.InvokeAsync("JoinRoom", room.Code, "Azubi1");
        await joined;

        var q1 = WaitFor<QuestionStartedMessage>(player, "QuestionStarted");
        var a1 = WaitFor<AnswerAcceptedMessage>(player, "AnswerAccepted");
        var end1 = WaitFor<QuestionEndedMessage>(player, "QuestionEnded");
        var board1 = WaitFor<LeaderboardMessage>(player, "Leaderboard");

        await host.InvokeAsync("StartGame");
        var first = await q1;
        Assert.Equal(0, first.Index);
        Assert.Equal(2, first.TotalQuestions);

        await player.InvokeAsync("SubmitAnswer", 0);
        Assert.True((await a1).Ok);
        await end1;
        var lb1 = await board1;
        Assert.True(lb1.HasMoreQuestions);

        var q2 = WaitFor<QuestionStartedMessage>(player, "QuestionStarted");
        var a2 = WaitFor<AnswerAcceptedMessage>(player, "AnswerAccepted");
        var end2 = WaitFor<QuestionEndedMessage>(player, "QuestionEnded");
        var board2 = WaitFor<LeaderboardMessage>(player, "Leaderboard");
        var finished = WaitFor<GameFinishedMessage>(player, "GameFinished");

        await host.InvokeAsync("NextQuestion");
        var second = await q2;
        Assert.Equal(1, second.Index);

        await player.InvokeAsync("SubmitAnswer", 1);
        Assert.True((await a2).Ok);
        await end2;
        var lb2 = await board2;
        Assert.False(lb2.HasMoreQuestions);

        await host.InvokeAsync("NextQuestion");
        var done = await finished;
        Assert.Contains(done.Entries, e => e.Nickname == "Azubi1");
    }

    private async Task<Guid> SeedTwoQuestionQuizAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.NewGuid();
        db.Quizzes.Add(new Quiz
        {
            Id = id,
            Title = "Phase2 Round",
            Questions =
            [
                new Question
                {
                    Id = Guid.NewGuid(),
                    QuizId = id,
                    Text = "First?",
                    Option0 = "A",
                    Option1 = "B",
                    Option2 = "C",
                    Option3 = "D",
                    CorrectIndex = 0,
                    TimeLimitSeconds = 30,
                    SortOrder = 0
                },
                new Question
                {
                    Id = Guid.NewGuid(),
                    QuizId = id,
                    Text = "Second?",
                    Option0 = "A",
                    Option1 = "B",
                    Option2 = "C",
                    Option3 = "D",
                    CorrectIndex = 1,
                    TimeLimitSeconds = 30,
                    SortOrder = 1
                }
            ]
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
                PropertyNameCaseInsensitive = true
            });
            if (value is not null)
            {
                tcs.TrySetResult(value);
            }
        });
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(8));
    }
}
