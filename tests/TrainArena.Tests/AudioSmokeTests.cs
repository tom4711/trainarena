using System.Net;

namespace TrainArena.Tests;

public class AudioSmokeTests : IClassFixture<TrainArenaWebAppFactory>
{
    private readonly HttpClient _client;

    public AudioSmokeTests(TrainArenaWebAppFactory factory) => _client = factory.CreateClient();

    [Theory]
    [InlineData("/audio/bgm-host.ogg")]
    [InlineData("/audio/sfx-correct.ogg")]
    [InlineData("/audio/sfx-wrong.ogg")]
    [InlineData("/audio/sfx-powerup.ogg")]
    [InlineData("/audio/sfx-hit.ogg")]
    [InlineData("/audio/sfx-block.ogg")]
    public async Task AudioAssets_AreServed(string path)
    {
        var res = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True((res.Content.Headers.ContentLength ?? 0) > 100);
    }

    [Fact]
    public async Task AudioJs_ExposesHostAndPlayerHelpers()
    {
        var res = await _client.GetAsync("/js/audio.js");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var js = await res.Content.ReadAsStringAsync();
        Assert.Contains("TrainArenaAudio", js, StringComparison.Ordinal);
        Assert.Contains("createAudioController", js, StringComparison.Ordinal);
        Assert.Contains("bindHostControls", js, StringComparison.Ordinal);
        Assert.Contains("bindPlayerMute", js, StringComparison.Ordinal);
        Assert.Contains("playAnswerResult", js, StringComparison.Ordinal);
        Assert.Contains("playPowerUpFx", js, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HostPage_IncludesAudioControlsAndScript()
    {
        var res = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("id=\"btn-audio-mute\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"audio-volume\"", html, StringComparison.Ordinal);
        Assert.Contains("/js/audio.js", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlayerPage_IncludesSfxMuteAndScript()
    {
        var res = await _client.GetAsync("/player/");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("id=\"btn-audio-mute\"", html, StringComparison.Ordinal);
        Assert.Contains("/js/audio.js", html, StringComparison.Ordinal);
    }
}
