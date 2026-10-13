using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainArena.Contracts;
using TrainArena.Data;
using TrainArena.Data.Entities;

namespace TrainArena.Tests;

public class AsyncRaceHubTests : IClassFixture<TrainArenaWebAppFactory>
{
    private readonly TrainArenaWebAppFactory _factory;

    public AsyncRaceHubTests(TrainArenaWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task StartGame_TwoPlayers_PerPlayerQuestions_AndScoreboardOnSubmit()
    {
        var quizId = await SeedMultiQuestionQuizAsync();

        await using var host = await ConnectAsync();
        await using var ada = await ConnectAsync();
        await using var bob = await ConnectAsync();

        var roomCreated = WaitFor<RoomCreatedMessage>(host, "RoomCreated");
        await host.InvokeAsync(
            "CreateRoom",
            quizId,
            null,
            null,
            null,
            new AsyncRaceConfigDto(true));
        var room = await roomCreated;

        var joinAda = WaitFor<PlayerJoinedMessage>(host, "PlayerJoined");
        await ada.InvokeAsync("JoinRoom", room.Code, "Ada");
        await joinAda;
        var joinBob = WaitFor<PlayerJoinedMessage>(host, "PlayerJoined");
        await bob.InvokeAsync("JoinRoom", room.Code, "Bob");
        await joinBob;

        var adaQ = WaitFor<QuestionStartedMessage>(ada, "QuestionStarted");
        var bobQ = WaitFor<QuestionStartedMessage>(bob, "QuestionStarted");
        var hostBoard = WaitFor<AsyncScoreboardMessage>(host, "AsyncScoreboard");

        await host.InvokeAsync("StartGame");

        var adaFirst = await adaQ;
        var bobFirst = await bobQ;
        Assert.Equal(0, adaFirst.Index);
        Assert.Equal(0, bobFirst.Index);
        Assert.Equal(3, adaFirst.TotalQuestions);
        Assert.Equal(3, bobFirst.TotalQuestions);
        Assert.NotEmpty(adaFirst.Text);
        Assert.NotEmpty(bobFirst.Text);

        var board0 = await hostBoard;
        Assert.Equal(2, board0.Entries.Count);
        Assert.All(board0.Entries, e => Assert.Equal(0, e.AnsweredCount));

        var adaAccepted = WaitFor<AnswerAcceptedMessage>(ada, "AnswerAccepted");
        var adaEnded = WaitFor<QuestionEndedMessage>(ada, "QuestionEnded");
        var adaNext = WaitFor<QuestionStartedMessage>(ada, "QuestionStarted");
        var boardAfter = WaitFor<AsyncScoreboardMessage>(host, "AsyncScoreboard");

        await ada.InvokeAsync("SubmitAnswer", 0);
        var accepted = await adaAccepted;
        Assert.True(accepted.Ok);
        await adaEnded;
        var adaSecond = await adaNext;
        Assert.Equal(1, adaSecond.Index);

        var board1 = await boardAfter;
        var adaEntry = board1.Entries.First(e =>
            string.Equals(e.Nickname, "Ada", StringComparison.OrdinalIgnoreCase));
        var bobEntry = board1.Entries.First(e =>
            string.Equals(e.Nickname, "Bob", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, adaEntry.AnsweredCount);
        Assert.Equal(0, bobEntry.AnsweredCount);
    }

    [Fact]
    public async Task CreateRoom_ExamAndAsyncRace_RejectsWithJoinError()
    {
        var quizId = await SeedMultiQuestionQuizAsync();
        await using var host = await ConnectAsync();
        var err = WaitFor<JoinErrorMessage>(host, "JoinError");
        await host.InvokeAsync(
            "CreateRoom",
            quizId,
            null,
            null,
            new ExamConfigDto(true, null),
            new AsyncRaceConfigDto(true));
        var message = await err;
        Assert.Contains("nicht kombinierbar", message.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRoom_AutoAdvanceAndAsyncRace_RejectsWithJoinError()
    {
        var quizId = await SeedMultiQuestionQuizAsync();
        await using var host = await ConnectAsync();
        var err = WaitFor<JoinErrorMessage>(host, "JoinError");
        await host.InvokeAsync(
            "CreateRoom",
            quizId,
            null,
            new AutoAdvanceConfigDto(true, 5),
            null,
            new AsyncRaceConfigDto(true));
        var message = await err;
        Assert.Contains("nicht kombinierbar", message.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejoinRoom_DuringAsyncRace_RestoresCurrentQuestion()
    {
        var quizId = await SeedMultiQuestionQuizAsync();

        await using var host = await ConnectAsync();
        await using var ada = await ConnectAsync();

        var roomCreated = WaitFor<RoomCreatedMessage>(host, "RoomCreated");
        await host.InvokeAsync(
            "CreateRoom",
            quizId,
            null,
            null,
            null,
            new AsyncRaceConfigDto(true));
        var room = await roomCreated;

        await ada.InvokeAsync("JoinRoom", room.Code, "Ada");

        var adaQ = WaitFor<QuestionStartedMessage>(ada, "QuestionStarted");
        await host.InvokeAsync("StartGame");
        var first = await adaQ;

        await ada.DisposeAsync();

        await using var ada2 = await ConnectAsync();
        var restored = WaitFor<QuestionStartedMessage>(ada2, "QuestionStarted");
        await ada2.InvokeAsync("RejoinRoom", room.Code, "Ada");
        var again = await restored;
        Assert.Equal(first.Index, again.Index);
        Assert.Equal(first.Text, again.Text);
        Assert.Equal(first.TotalQuestions, again.TotalQuestions);
    }

    private async Task<Guid> SeedMultiQuestionQuizAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.NewGuid();
        db.Quizzes.Add(new Quiz
        {
            Id = id,
            Title = "Async Race",
            Questions =
            [
                new Question
                {
                    Id = Guid.NewGuid(),
                    QuizId = id,
                    Text = "Alpha?",
                    Option0 = "A",
                    Option1 = "B",
                    CorrectIndex = 0,
                    TimeLimitSeconds = 30,
                    SortOrder = 0
                },
                new Question
                {
                    Id = Guid.NewGuid(),
                    QuizId = id,
                    Text = "Beta?",
                    Option0 = "A",
                    Option1 = "B",
                    CorrectIndex = 0,
                    TimeLimitSeconds = 30,
                    SortOrder = 1
                },
                new Question
                {
                    Id = Guid.NewGuid(),
                    QuizId = id,
                    Text = "Gamma?",
                    Option0 = "A",
                    Option1 = "B",
                    CorrectIndex = 0,
                    TimeLimitSeconds = 30,
                    SortOrder = 2
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
