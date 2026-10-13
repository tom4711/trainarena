using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TrainArena.Tests;

public class ShutdownApiTests
{
    [Fact]
    public async Task HostPage_ShowsShutdownButton()
    {
        await using var factory = new TrainArenaWebAppFactory();
        var client = factory.CreateClient();
        var html = await client.GetStringAsync("/");
        Assert.Contains("btn-shutdown", html, StringComparison.Ordinal);
        Assert.Contains("Server beenden", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shutdown_ReturnsStopping()
    {
        // Dedicated factory — StopApplication must not poison shared fixtures.
        await using var factory = new TrainArenaWebAppFactory();
        var client = factory.CreateClient();
        var res = await client.PostAsync("/api/shutdown", content: null);
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(
            res.StatusCode == HttpStatusCode.OK,
            $"Expected OK from /api/shutdown, got {(int)res.StatusCode} {res.StatusCode}. Body: {body}");
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal("stopping", json.GetProperty("status").GetString());
    }
}
