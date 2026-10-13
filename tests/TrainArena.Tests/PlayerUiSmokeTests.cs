using System.Net;

namespace TrainArena.Tests;

public class PlayerUiSmokeTests : IClassFixture<TrainArenaWebAppFactory>
{
    private readonly HttpClient _client;

    public PlayerUiSmokeTests(TrainArenaWebAppFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task PlayerIndex_HasTimerTrackHook()
    {
        var res = await _client.GetAsync("/player/");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("id=\"q-timer-track\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"q-meta\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"powerups\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlayerCss_DefinesTimerTrackAndPowerupBadge()
    {
        var res = await _client.GetAsync("/player/player.css");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var css = await res.Content.ReadAsStringAsync();
        Assert.Contains(".q-timer-track", css, StringComparison.Ordinal);
        Assert.Contains("--timer-pct", css, StringComparison.Ordinal);
        Assert.Contains(".powerups button .count", css, StringComparison.Ordinal);
        Assert.Contains("border-radius: 999px", css, StringComparison.Ordinal); // chip/badge shape signal
    }
}
