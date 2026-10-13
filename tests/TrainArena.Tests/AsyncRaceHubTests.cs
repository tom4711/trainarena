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

        await ada.InvokeAsync("SubmitAnswer", 0, 0);
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
    public async Task AsyncTimeout_AdvancesPlayerAndUpdatesScoreboard()
    {
        var quizId = await SeedTwoQuestionShortLimitQuizAsync();

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

        var adaFirst = WaitFor<QuestionStartedMessage>(ada, "QuestionStarted");
        var hostBoard0 = WaitFor<AsyncScoreboardMessage>(host, "AsyncScoreboard");
        await host.InvokeAsync("StartGame");
        await adaFirst;
        await hostBoard0;

        var timedOut = WaitFor<QuestionEndedMessage>(ada, "QuestionEnded", timeoutSeconds: 15);
        var nextQ = WaitFor<QuestionStartedMessage>(ada, "QuestionStarted", timeoutSeconds: 15);
        var boardAfter = WaitFor<AsyncScoreboardMessage>(host, "AsyncScoreboard", timeoutSeconds: 15);

        var ended = await timedOut;
        Assert.Equal(0, ended.Index);

        var second = await nextQ.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, second.Index);

        var board = await boardAfter.WaitAsync(TimeSpan.FromSeconds(2));
        var adaEntry = board.Entries.First(e =>
            string.Equals(e.Nickname, "Ada", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, adaEntry.AnsweredCount);
    }

    [Fact]
    public async Task AsyncTimeout_DisconnectedLastPlayerFinishesRace_BroadcastsGameFinished()
    {
        var quizId = await SeedSingleQuestionQuizAsync(timeLimitSeconds: 1);

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

        await ada.InvokeAsync("JoinRoom", room.Code, "Ada");
        await bob.InvokeAsync("JoinRoom", room.Code, "Bob");

        _ = WaitFor<QuestionStartedMessage>(ada, "QuestionStarted");
        _ = WaitFor<QuestionStartedMessage>(bob, "QuestionStarted");
        await host.InvokeAsync("StartGame");

        var adaEnded = WaitFor<QuestionEndedMessage>(ada, "QuestionEnded");
        await ada.InvokeAsync("SubmitAnswer", 0, 0);
        await adaEnded;

        await bob.DisposeAsync();

        var finished = WaitFor<GameFinishedMessage>(host, "GameFinished", timeoutSeconds: 15);
        var gameOver = await finished;
        Assert.Empty(gameOver.Review);
        Assert.Equal(2, gameOver.Entries.Count);
    }

    [Fact]
    public async Task AsyncRace_LastPlayerFinishes_BroadcastsGameFinished()
    {
        var quizId = await SeedSingleQuestionQuizAsync();

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

        await ada.InvokeAsync("JoinRoom", room.Code, "Ada");
        await bob.InvokeAsync("JoinRoom", room.Code, "Bob");

        _ = WaitFor<QuestionStartedMessage>(ada, "QuestionStarted");
        _ = WaitFor<QuestionStartedMessage>(bob, "QuestionStarted");
        await host.InvokeAsync("StartGame");

        var finished = WaitForGameFinishedWithCountAsync(host);
        await ada.InvokeAsync("SubmitAnswer", 0, 0);
        await bob.InvokeAsync("SubmitAnswer", 0, 0);

        var (gameOver, finishCount) = await finished.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.Equal(1, finishCount);
        Assert.Empty(gameOver.Review);
        Assert.Equal(2, gameOver.Entries.Count);
    }

    [Fact]
    public async Task StartGame_HostDoesNotReceiveQuestionStartedWithText()
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

        var hostQuestionStarted = 0;
        host.On<JsonElement>("QuestionStarted", _ => Interlocked.Increment(ref hostQuestionStarted));

        _ = WaitFor<QuestionStartedMessage>(ada, "QuestionStarted");
        await host.InvokeAsync("StartGame");
        await Task.Delay(500);

        Assert.Equal(0, hostQuestionStarted);
    }

    [Fact]
    public async Task RejoinHost_DuringAsyncRace_ReceivesScoreboard()
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
        var hostBoard = WaitFor<AsyncScoreboardMessage>(host, "AsyncScoreboard");
        await host.InvokeAsync("StartGame");
        await adaQ;
        await hostBoard;

        await host.DisposeAsync();

        await using var host2 = await ConnectAsync();
        var board = WaitFor<AsyncScoreboardMessage>(host2, "AsyncScoreboard");
        await host2.InvokeAsync("RejoinHost", room.Code);
        var restored = await board;
        Assert.Single(restored.Entries);
    }

    [Fact]
    public async Task RejoinRoom_AfterPlayerFinishedWhileOthersRace_ScoreboardAndQuestionEnded()
    {
        var quizId = await SeedSingleQuestionQuizAsync();

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

        await ada.InvokeAsync("JoinRoom", room.Code, "Ada");
        await bob.InvokeAsync("JoinRoom", room.Code, "Bob");

        _ = WaitFor<QuestionStartedMessage>(ada, "QuestionStarted");
        _ = WaitFor<QuestionStartedMessage>(bob, "QuestionStarted");
        await host.InvokeAsync("StartGame");

        var adaEnded = WaitFor<QuestionEndedMessage>(ada, "QuestionEnded");
        await ada.InvokeAsync("SubmitAnswer", 0, 0);
        await adaEnded;

        await ada.DisposeAsync();

        await using var ada2 = await ConnectAsync();
        var board = WaitFor<AsyncScoreboardMessage>(ada2, "AsyncScoreboard");
        var endedAgain = WaitFor<QuestionEndedMessage>(ada2, "QuestionEnded");
        await ada2.InvokeAsync("RejoinRoom", room.Code, "Ada");

        var scoreboard = await board;
        var ended = await endedAgain;
        Assert.Equal(1, scoreboard.Entries.Count(e =>
            string.Equals(e.Nickname, "Ada", StringComparison.OrdinalIgnoreCase) && e.AnsweredCount == 1));
        Assert.Equal(0, ended.Index);
    }

    [Fact]
    public async Task RejoinHost_AfterAsyncRaceFinished_UsesEmptyReviewPayload()
    {
        var quizId = await SeedSingleQuestionQuizAsync();

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
        _ = WaitFor<QuestionStartedMessage>(ada, "QuestionStarted");
        await host.InvokeAsync("StartGame");

        var liveFinish = WaitFor<GameFinishedMessage>(host, "GameFinished");
        await ada.InvokeAsync("SubmitAnswer", 0, 0);
        await liveFinish;

        await host.DisposeAsync();

        await using var host2 = await ConnectAsync();
        var finished = WaitFor<GameFinishedMessage>(host2, "GameFinished");
        await host2.InvokeAsync("RejoinHost", room.Code);
        var payload = await finished;
        Assert.Empty(payload.Review);
    }

    [Fact]
    public async Task ExtraTime_InAsyncRace_DoesNotBroadcastEndsAtToOthers()
    {
        var quizId = await SeedMultiQuestionQuizAsync();

        await using var host = await ConnectAsync();
        await using var ada = await ConnectAsync();
        await using var bob = await ConnectAsync();

        var roomCreated = WaitFor<RoomCreatedMessage>(host, "RoomCreated");
        await host.InvokeAsync(
            "CreateRoom",
            quizId,
            new PowerUpConfigDto(true, new Dictionary<string, int> { ["extra_time"] = 1 }, 2, 3, true, 1),
            null,
            null,
            new AsyncRaceConfigDto(true));
        var room = await roomCreated;

        await ada.InvokeAsync("JoinRoom", room.Code, "Ada");
        await bob.InvokeAsync("JoinRoom", room.Code, "Bob");

        var adaQ = WaitFor<QuestionStartedMessage>(ada, "QuestionStarted");
        _ = WaitFor<QuestionStartedMessage>(bob, "QuestionStarted");
        await host.InvokeAsync("StartGame");
        await adaQ;

        var bobEndsAtReceived = 0;
        bob.On<PowerUpUsedMessage>("PowerUpUsed", (msg) =>
        {
            if (msg.EndsAtUtc is not null)
            {
                Interlocked.Increment(ref bobEndsAtReceived);
            }
        });

        var adaUsed = WaitFor<PowerUpUsedMessage>(ada, "PowerUpUsed");
        await ada.InvokeAsync("UsePowerUp", "extra_time", null);
        var used = await adaUsed;
        Assert.True(used.Ok);
        Assert.NotNull(used.EndsAtUtc);

        await Task.Delay(300);
        Assert.Equal(0, bobEndsAtReceived);
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
        var scoreboardFirst = WaitFor<AsyncScoreboardMessage>(ada2, "AsyncScoreboard");
        var restored = WaitFor<QuestionStartedMessage>(ada2, "QuestionStarted");
        await ada2.InvokeAsync("RejoinRoom", room.Code, "Ada");
        var board = await scoreboardFirst;
        var again = await restored;
        Assert.NotEmpty(board.Entries);
        Assert.Equal(first.Index, again.Index);
        Assert.Equal(first.Text, again.Text);
        Assert.Equal(first.TotalQuestions, again.TotalQuestions);
    }

    [Fact]
    public async Task SubmitAnswer_NextQuestionStarted_ArrivesWithoutHubFeedbackDelay()
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

        var firstQ = WaitFor<QuestionStartedMessage>(ada, "QuestionStarted");
        await host.InvokeAsync("StartGame");
        await firstQ;

        var endedTask = WaitFor<QuestionEndedMessage>(ada, "QuestionEnded");
        var nextTask = WaitFor<QuestionStartedMessage>(ada, "QuestionStarted");
        await ada.InvokeAsync("SubmitAnswer", 0, 0);
        await endedTask;
        var endedAt = DateTimeOffset.UtcNow;
        var next = await nextTask;
        var nextStartedAt = DateTimeOffset.UtcNow;

        Assert.Equal(1, next.Index);
        var hubGapMs = (nextStartedAt - endedAt).TotalMilliseconds;
        Assert.True(hubGapMs < 500, $"QuestionStarted took {hubGapMs}ms after QuestionEnded (hub delay must be client-side)");

        var remainingMs = (next.EndsAtUtc - DateTimeOffset.UtcNow).TotalMilliseconds;
        Assert.True(remainingMs > 25_000, $"EndsAtUtc should reflect full timer, not post-delay stamp (remaining {remainingMs}ms)");
    }

    [Fact]
    public async Task AsyncTimeout_TwoPlayers_BothAdvanceWithoutSerializedHubDelay()
    {
        var quizId = await SeedTwoQuestionShortLimitQuizAsync();

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

        await ada.InvokeAsync("JoinRoom", room.Code, "Ada");
        await bob.InvokeAsync("JoinRoom", room.Code, "Bob");

        _ = WaitFor<QuestionStartedMessage>(ada, "QuestionStarted");
        _ = WaitFor<QuestionStartedMessage>(bob, "QuestionStarted");
        await host.InvokeAsync("StartGame");

        var adaQ2 = WaitFor<QuestionStartedMessage>(ada, "QuestionStarted", timeoutSeconds: 15);
        var bobQ2 = WaitFor<QuestionStartedMessage>(bob, "QuestionStarted", timeoutSeconds: 15);

        var adaSecond = await adaQ2;
        var bobSecond = await bobQ2;

        Assert.Equal(1, adaSecond.Index);
        Assert.Equal(1, bobSecond.Index);
    }

    private Task<Guid> SeedSingleQuestionQuizAsync(int timeLimitSeconds = 30) =>
        SeedSingleQuestionQuizInternalAsync(timeLimitSeconds);

    private async Task<Guid> SeedSingleQuestionQuizInternalAsync(int timeLimitSeconds)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.NewGuid();
        db.Quizzes.Add(new Quiz
        {
            Id = id,
            Title = "Async One",
            Questions =
            [
                new Question
                {
                    Id = Guid.NewGuid(),
                    QuizId = id,
                    Text = "Only?",
                    Option0 = "A",
                    Option1 = "B",
                    CorrectIndex = 0,
                    TimeLimitSeconds = timeLimitSeconds,
                    SortOrder = 0
                }
            ]
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> SeedTwoQuestionShortLimitQuizAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.NewGuid();
        db.Quizzes.Add(new Quiz
        {
            Id = id,
            Title = "Async Timeout",
            Questions =
            [
                new Question
                {
                    Id = Guid.NewGuid(),
                    QuizId = id,
                    Text = "Fast?",
                    Option0 = "A",
                    Option1 = "B",
                    CorrectIndex = 0,
                    TimeLimitSeconds = 1,
                    SortOrder = 0
                },
                new Question
                {
                    Id = Guid.NewGuid(),
                    QuizId = id,
                    Text = "Slow?",
                    Option0 = "A",
                    Option1 = "B",
                    CorrectIndex = 0,
                    TimeLimitSeconds = 1,
                    SortOrder = 1
                }
            ]
        });
        await db.SaveChangesAsync();
        return id;
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

    private static Task<T> WaitFor<T>(HubConnection connection, string methodName, double timeoutSeconds = 8)
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
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(timeoutSeconds));
    }

    private static Task<(GameFinishedMessage Message, int Count)> WaitForGameFinishedWithCountAsync(
        HubConnection connection,
        double timeoutSeconds = 8,
        double settleSeconds = 0.5)
    {
        var tcs = new TaskCompletionSource<GameFinishedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        connection.On<JsonElement>("GameFinished", payload =>
        {
            count++;
            var value = payload.Deserialize<GameFinishedMessage>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (value is not null)
            {
                tcs.TrySetResult(value);
            }
        });
        return tcs.Task
            .WaitAsync(TimeSpan.FromSeconds(timeoutSeconds))
            .ContinueWith(
                async t =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(settleSeconds));
                    return (await t, count);
                },
                TaskScheduler.Default)
            .Unwrap();
    }
}
