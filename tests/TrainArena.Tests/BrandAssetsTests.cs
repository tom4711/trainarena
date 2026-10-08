using System.Net;
using System.Text;
using System.Xml.Linq;

namespace TrainArena.Tests;

public class BrandAssetsTests : IClassFixture<TrainArenaWebAppFactory>
{
    private readonly HttpClient _client;

    public BrandAssetsTests(TrainArenaWebAppFactory factory) => _client = factory.CreateClient();

    internal static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "TrainArena.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Could not find repository root (TrainArena.sln).");
    }

    private static void AssertSvgIsWellFormedUtf8(string path)
    {
        Assert.True(File.Exists(path), $"Missing {path}");
        var bytes = File.ReadAllBytes(path);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        _ = utf8.GetString(bytes);
        XDocument.Load(path);
    }

    [Fact]
    public void LogoIconSvg_ExistsOnDisk()
    {
        var path = Path.Combine(RepoRoot(), "src", "TrainArena", "wwwroot", "assets", "brand", "logo-icon.svg");
        Assert.True(File.Exists(path), $"Missing {path}");
        var svg = File.ReadAllText(path);
        Assert.Contains("TrainArena", svg);
        Assert.Contains("#0032C3", svg);
        Assert.Contains("#0ACDDE", svg);
    }

    [Fact]
    public void LogoIconSvg_IsWellFormedUtf8() =>
        AssertSvgIsWellFormedUtf8(Path.Combine(RepoRoot(), "src", "TrainArena", "wwwroot", "assets", "brand", "logo-icon.svg"));

    [Fact]
    public void BannerSvg_IsWellFormedUtf8()
    {
        var path = Path.Combine(RepoRoot(), "docs", "brand", "banner.svg");
        AssertSvgIsWellFormedUtf8(path);
        var text = File.ReadAllText(path);
        Assert.Contains("Live-Quiz für Ausbildung", text);
    }

    [Fact]
    public async Task LogoIconSvg_IsServed()
    {
        var res = await _client.GetAsync("/assets/brand/logo-icon.svg");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("svg", res.Content.Headers.ContentType?.MediaType ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LogoIconPng_ExistsOnDisk()
    {
        var path = Path.Combine(RepoRoot(), "src", "TrainArena", "wwwroot", "assets", "brand", "logo-icon.png");
        Assert.True(File.Exists(path), $"Missing {path}");
        Assert.True(new FileInfo(path).Length > 1000);
    }

    [Fact]
    public async Task LogoIconPng_IsServed()
    {
        var res = await _client.GetAsync("/assets/brand/logo-icon.png");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("png", res.Content.Headers.ContentType?.MediaType ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FaviconPng_ExistsOnDisk()
    {
        var path = Path.Combine(RepoRoot(), "src", "TrainArena", "wwwroot", "assets", "brand", "favicon.png");
        Assert.True(File.Exists(path), $"Missing {path}");
        Assert.True(new FileInfo(path).Length > 100);
    }

    [Fact]
    public async Task FaviconPng_IsServed()
    {
        var res = await _client.GetAsync("/assets/brand/favicon.png");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
