using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TrainArena.Tests;

public class AppVersionTests : IClassFixture<TrainArenaWebAppFactory>
{
    private readonly HttpClient _client;

    public AppVersionTests(TrainArenaWebAppFactory factory) => _client = factory.CreateClient();

    [Fact]
    public void Display_IsNonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppVersion.Display));
    }

    [Fact]
    public async Task Health_IncludesVersion()
    {
        var res = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", json.GetProperty("status").GetString());
        var version = json.GetProperty("version").GetString();
        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.Equal(AppVersion.Display, version);
    }

    [Fact]
    public async Task HostPage_ShowsAppVersion()
    {
        var html = await _client.GetStringAsync("/");
        Assert.Contains("app-version", html, StringComparison.Ordinal);
        Assert.Contains($"v{AppVersion.Display}", html, StringComparison.Ordinal);
    }
}
