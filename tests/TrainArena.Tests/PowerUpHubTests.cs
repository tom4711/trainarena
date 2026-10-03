using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using TrainArena.Contracts;
using TrainArena.Data;

namespace TrainArena.Tests;

public class PowerUpHubTests : IClassFixture<TrainArenaWebAppFactory>
{
    private readonly TrainArenaWebAppFactory _factory;

    public PowerUpHubTests(TrainArenaWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UsePowerUp_FiftyFifty_EmitsPowerUpUsedAndDepletesInventory()
    {
        await using var host = await ConnectAsync();
        await using var player = await ConnectAsync();

        var hostRoomCreated = WaitFor<RoomCreatedMessage>(host, "RoomCreated");
        await host.InvokeAsync("CreateRoom", SeedData.AusbildungBasicsQuizId, null, null);
        var room = await hostRoomCreated;

        var playerInventoryOnJoin = WaitFor<InventoryUpdateMessage>(player, "InventoryUpdate");
        await player.InvokeAsync("JoinRoom", room.Code, "Azubi1");
        var inventoryOnJoin = await playerInventoryOnJoin;
        Assert.Equal(1, inventoryOnJoin.Counts["fifty_fifty"]);

        var playerQuestion = WaitFor<QuestionStartedMessage>(player, "QuestionStarted");
        await host.InvokeAsync("StartGame");
        await playerQuestion;

        var powerUpUsed = WaitFor<PowerUpUsedMessage>(player, "PowerUpUsed");
        var inventoryAfterUse = WaitFor<InventoryUpdateMessage>(player, "InventoryUpdate");
        await player.InvokeAsync("UsePowerUp", "fifty_fifty");

        var used = await powerUpUsed;
        Assert.True(used.Ok);
        Assert.Equal("fifty_fifty", used.PowerUpId);
        Assert.NotNull(used.MaskedWrongIndexes);
        Assert.Equal(2, used.MaskedWrongIndexes!.Length);

        var inventory = await inventoryAfterUse;
        Assert.Equal(0, inventory.Counts["fifty_fifty"]);
    }

    [Fact]
    public async Task UsePowerUp_AfterAnswer_EmitsPowerUpError()
    {
        await using var host = await ConnectAsync();
        await using var player1 = await ConnectAsync();
        await using var player2 = await ConnectAsync();

        var hostRoomCreated = WaitFor<RoomCreatedMessage>(host, "RoomCreated");
        await host.InvokeAsync("CreateRoom", SeedData.AusbildungBasicsQuizId, null, null);
        var room = await hostRoomCreated;

        await player1.InvokeAsync("JoinRoom", room.Code, "Azubi1");
        await player2.InvokeAsync("JoinRoom", room.Code, "Azubi2");

        var q1 = WaitFor<QuestionStartedMessage>(player1, "QuestionStarted");
        await host.InvokeAsync("StartGame");
        await q1;

        var answerAccepted = WaitFor<AnswerAcceptedMessage>(player1, "AnswerAccepted");
        await player1.InvokeAsync("SubmitAnswer", 0);
        await answerAccepted;

        var powerUpError = WaitFor<PowerUpErrorMessage>(player1, "PowerUpError");
        await player1.InvokeAsync("UsePowerUp", "double");
        var err = await powerUpError;

        Assert.Contains("answered", err.Error, StringComparison.OrdinalIgnoreCase);
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
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
