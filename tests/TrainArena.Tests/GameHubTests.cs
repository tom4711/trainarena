using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using TrainArena.Contracts;
using TrainArena.Data;

namespace TrainArena.Tests;

public class GameHubTests : IClassFixture<TrainArenaWebAppFactory>
{
    private readonly TrainArenaWebAppFactory _factory;

    public GameHubTests(TrainArenaWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateRoom_ThenJoin_BroadcastsLobbyStateToBothConnections()
    {
        await using var host = await ConnectAsync();
        await using var player = await ConnectAsync();

        var hostRoomCreated = WaitFor<RoomCreatedMessage>(host, "RoomCreated");
        var hostLobby = WaitFor<LobbyStateMessage>(host, "LobbyState");

        await host.InvokeAsync("CreateRoom", SeedData.AusbildungBasicsQuizId, null, null);
        var room = await hostRoomCreated;
        Assert.Matches("^[A-Z0-9]{6}$", room.Code);
        await hostLobby; // initial empty lobby for host

        var hostPlayerJoined = WaitFor<PlayerJoinedMessage>(host, "PlayerJoined");
        var hostLobbyAfterJoin = WaitFor<LobbyStateMessage>(host, "LobbyState");
        var playerJoined = WaitFor<PlayerJoinedMessage>(player, "PlayerJoined");
        var playerLobby = WaitFor<LobbyStateMessage>(player, "LobbyState");

        await player.InvokeAsync("JoinRoom", room.Code, "Azubi1");

        var joined = await playerJoined;
        Assert.Equal("Azubi1", joined.Nickname);
        Assert.Equal(1, joined.ConnectedCount);

        var hostJoinedEvent = await hostPlayerJoined;
        Assert.Equal("Azubi1", hostJoinedEvent.Nickname);

        var lobby = await playerLobby;
        Assert.Equal(1, lobby.ConnectedCount);
        Assert.Contains(lobby.Players, p => p.Nickname == "Azubi1");

        var hostSeenLobby = await hostLobbyAfterJoin;
        Assert.Equal(1, hostSeenLobby.ConnectedCount);
        Assert.Contains(hostSeenLobby.Players, p => p.Nickname == "Azubi1");
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
