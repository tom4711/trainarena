using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using TrainArena.Contracts;

namespace TrainArena.Tests;

public class DemoRoundTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DemoRoundTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StartDemo_SubmitAnswer_ShowsLeaderboard_ThenHostNextFinishes()
    {
        await using var host = await ConnectAsync();
        await using var player = await ConnectAsync();

        var roomCreated = WaitFor<RoomCreatedMessage>(host, "RoomCreated");
        await host.InvokeAsync("CreateRoom");
        var room = await roomCreated;

        var playerJoined = WaitFor<PlayerJoinedMessage>(host, "PlayerJoined");
        await player.InvokeAsync("JoinRoom", room.Code, "Azubi1");
        await playerJoined;

        var questionStarted = WaitFor<QuestionStartedMessage>(player, "QuestionStarted");
        var answerAccepted = WaitFor<AnswerAcceptedMessage>(player, "AnswerAccepted");
        var questionEnded = WaitFor<QuestionEndedMessage>(player, "QuestionEnded");
        var leaderboard = WaitFor<LeaderboardMessage>(player, "Leaderboard");
        var finished = WaitFor<GameFinishedMessage>(player, "GameFinished");

        await host.InvokeAsync("StartDemo");
        var q = await questionStarted;
        Assert.Equal(4, q.Options.Length);

        // SeedData correct index = 2 ("16")
        await player.InvokeAsync("SubmitAnswer", 2);
        var accepted = await answerAccepted;
        Assert.True(accepted.Ok, accepted.Error);
        Assert.True(accepted.Points > 0);

        await questionEnded;
        var board = await leaderboard;
        Assert.Contains(board.Entries, e => e.Nickname == "Azubi1" && e.Score > 0);

        await host.InvokeAsync("NextQuestion");
        var done = await finished;
        Assert.Contains(done.Entries, e => e.Nickname == "Azubi1");
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
