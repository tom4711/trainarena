using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainArena.Contracts;
using TrainArena.Data;
using TrainArena.Data.Entities;

namespace TrainArena.Tests;

public class AutoAdvanceHubTests : IClassFixture<TrainArenaWebAppFactory>
{
    private readonly TrainArenaWebAppFactory _factory;

    public AutoAdvanceHubTests(TrainArenaWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Leaderboard_WithAutoAdvanceEnabled_BroadcastsScheduled()
    {
        var quizId = await SeedSingleQuestionQuizAsync();

        await using var host = await ConnectAsync();
        await using var player = await ConnectAsync();

        var roomCreated = WaitFor<RoomCreatedMessage>(host, "RoomCreated");
        await host.InvokeAsync(
            "CreateRoom",
            quizId,
            null,
            new AutoAdvanceConfigDto(true, 3));
        var room = await roomCreated;

        await player.InvokeAsync("JoinRoom", room.Code, "Azubi1");

        var q1 = WaitFor<QuestionStartedMessage>(player, "QuestionStarted");
        var scheduled = WaitFor<AutoAdvanceScheduledMessage>(player, "AutoAdvanceScheduled");
        var board = WaitFor<LeaderboardMessage>(player, "Leaderboard");

        await host.InvokeAsync("StartGame");
        await q1;
        await player.InvokeAsync("SubmitAnswer", 0);
        await board;

        var plan = await scheduled;
        Assert.True(plan.AdvancesAtUtc > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Leaderboard_AutoAdvanceEnabled_FiresNextQuestionAfterDelay()
    {
        var quizId = await SeedTwoQuestionQuizAsync();

        await using var host = await ConnectAsync();
        await using var player = await ConnectAsync();

        var roomCreated = WaitFor<RoomCreatedMessage>(host, "RoomCreated");
        await host.InvokeAsync(
            "CreateRoom",
            quizId,
            null,
            new AutoAdvanceConfigDto(true, 3));
        var room = await roomCreated;

        await player.InvokeAsync("JoinRoom", room.Code, "Azubi1");

        var q1 = WaitFor<QuestionStartedMessage>(player, "QuestionStarted");
        var scheduled = WaitFor<AutoAdvanceScheduledMessage>(player, "AutoAdvanceScheduled");
        var board1 = WaitFor<LeaderboardMessage>(player, "Leaderboard");

        await host.InvokeAsync("StartGame");
        await q1;
        await player.InvokeAsync("SubmitAnswer", 0);
        await board1;
        await scheduled;

        var q2 = WaitFor<QuestionStartedMessage>(player, "QuestionStarted");

        var second = await q2;
        Assert.Equal(1, second.Index);
    }

    [Fact]
    public async Task Leaderboard_WithAutoAdvanceDisabled_DoesNotBroadcastScheduled()
    {
        var quizId = await SeedTwoQuestionQuizAsync();

        await using var host = await ConnectAsync();
        await using var player = await ConnectAsync();

        var autoAdvanceScheduled = false;
        player.On<JsonElement>("AutoAdvanceScheduled", _ => autoAdvanceScheduled = true);

        var roomCreated = WaitFor<RoomCreatedMessage>(host, "RoomCreated");
        await host.InvokeAsync("CreateRoom", quizId, null, null);
        var room = await roomCreated;

        await player.InvokeAsync("JoinRoom", room.Code, "Azubi1");

        var q1 = WaitFor<QuestionStartedMessage>(player, "QuestionStarted");
        var board1 = WaitFor<LeaderboardMessage>(player, "Leaderboard");

        await host.InvokeAsync("StartGame");
        await q1;
        await player.InvokeAsync("SubmitAnswer", 0);
        await board1;

        await Task.Delay(TimeSpan.FromSeconds(2));
        Assert.False(autoAdvanceScheduled);
    }

    [Fact]
    public async Task HostNextQuestion_BeforeAutoAdvanceFire_CancelsAndDoesNotDoubleAdvance()
    {
        var quizId = await SeedTwoQuestionQuizAsync();

        await using var host = await ConnectAsync();
        await using var player = await ConnectAsync();

        var roomCreated = WaitFor<RoomCreatedMessage>(host, "RoomCreated");
        await host.InvokeAsync(
            "CreateRoom",
            quizId,
            null,
            new AutoAdvanceConfigDto(true, 3));
        var room = await roomCreated;

        await player.InvokeAsync("JoinRoom", room.Code, "Azubi1");

        var q1 = WaitFor<QuestionStartedMessage>(player, "QuestionStarted");
        var scheduled = WaitFor<AutoAdvanceScheduledMessage>(player, "AutoAdvanceScheduled");
        var board1 = WaitFor<LeaderboardMessage>(player, "Leaderboard");

        await host.InvokeAsync("StartGame");
        await q1;
        await player.InvokeAsync("SubmitAnswer", 0);
        await board1;
        await scheduled;

        var cancelled = WaitFor<AutoAdvanceCancelledMessage>(player, "AutoAdvanceCancelled");
        var q2 = WaitFor<QuestionStartedMessage>(player, "QuestionStarted");

        await host.InvokeAsync("NextQuestion");
        await cancelled;
        var second = await q2;
        Assert.Equal(1, second.Index);

        var extraQuestionStarted = false;
        player.On<JsonElement>("QuestionStarted", _ => extraQuestionStarted = true);

        await Task.Delay(TimeSpan.FromSeconds(4));
        Assert.False(extraQuestionStarted);
    }

    [Fact]
    public async Task HostDisconnect_CancelsPendingAutoAdvance()
    {
        var quizId = await SeedTwoQuestionQuizAsync();

        var host = await ConnectAsync();
        await using var player = await ConnectAsync();

        var roomCreated = WaitFor<RoomCreatedMessage>(host, "RoomCreated");
        await host.InvokeAsync(
            "CreateRoom",
            quizId,
            null,
            new AutoAdvanceConfigDto(true, 3));
        var room = await roomCreated;

        await player.InvokeAsync("JoinRoom", room.Code, "Azubi1");

        var q1 = WaitFor<QuestionStartedMessage>(player, "QuestionStarted");
        var scheduled = WaitFor<AutoAdvanceScheduledMessage>(player, "AutoAdvanceScheduled");
        var board1 = WaitFor<LeaderboardMessage>(player, "Leaderboard");

        await host.InvokeAsync("StartGame");
        await q1;
        await player.InvokeAsync("SubmitAnswer", 0);
        await board1;
        await scheduled;

        var cancelled = WaitFor<AutoAdvanceCancelledMessage>(player, "AutoAdvanceCancelled");
        await host.StopAsync();
        await host.DisposeAsync();
        await cancelled;

        var extraQuestionStarted = false;
        player.On<JsonElement>("QuestionStarted", _ => extraQuestionStarted = true);

        await Task.Delay(TimeSpan.FromSeconds(4));
        Assert.False(extraQuestionStarted);
    }

    [Fact]
    public async Task CreateRoom_InvalidAutoAdvanceDelay_EmitsJoinError()
    {
        await using var host = await ConnectAsync();

        var joinError = WaitFor<JoinErrorMessage>(host, "JoinError");
        await host.InvokeAsync(
            "CreateRoom",
            SeedData.AusbildungBasicsQuizId,
            null,
            new AutoAdvanceConfigDto(true, 7));

        var err = await joinError;
        Assert.Contains("3", err.Error);
    }

    private async Task<Guid> SeedSingleQuestionQuizAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.NewGuid();
        db.Quizzes.Add(new Quiz
        {
            Id = id,
            Title = "Auto-advance single",
            Questions =
            [
                new Question
                {
                    Id = Guid.NewGuid(),
                    QuizId = id,
                    Text = "Only?",
                    Option0 = "A",
                    Option1 = "B",
                    Option2 = "C",
                    Option3 = "D",
                    CorrectIndex = 0,
                    TimeLimitSeconds = 30,
                    SortOrder = 0
                }
            ]
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> SeedTwoQuestionQuizAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.NewGuid();
        db.Quizzes.Add(new Quiz
        {
            Id = id,
            Title = "Auto-advance round",
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
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
