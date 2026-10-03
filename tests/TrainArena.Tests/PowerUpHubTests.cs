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
        await host.InvokeAsync("CreateRoom", SeedData.AusbildungBasicsQuizId, null);
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
