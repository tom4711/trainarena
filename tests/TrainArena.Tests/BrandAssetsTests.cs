using System.Net;

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

    private static void AssertPngMaster(string relativePath, int expectedWidth, int expectedHeight)
    {
        var path = Path.Combine(RepoRoot(), relativePath);
        Assert.True(File.Exists(path), $"Missing {path}");
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == (byte)'P');
        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }

    [Fact]
    public void DocsLogoIconPng_IsMasterSize() =>
        AssertPngMaster(Path.Combine("docs", "brand", "logo-icon.png"), 4096, 4096);

    [Fact]
    public void DocsBannerPng_IsMasterSize() =>
        AssertPngMaster(Path.Combine("docs", "brand", "banner.png"), 3840, 2160);

    [Fact]
    public void DocsLogoIconArenaPng_IsMasterSize() =>
        AssertPngMaster(Path.Combine("docs", "brand", "logo-icon-arena.png"), 4096, 4096);

    [Fact]
    public void SvgRedraws_AreNotPresent()
    {
        var root = RepoRoot();
        Assert.False(File.Exists(Path.Combine(root, "docs", "brand", "logo-icon.svg")));
        Assert.False(File.Exists(Path.Combine(root, "docs", "brand", "banner.svg")));
        Assert.False(File.Exists(Path.Combine(root, "src", "TrainArena", "wwwroot", "assets", "brand", "logo-icon.svg")));
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
