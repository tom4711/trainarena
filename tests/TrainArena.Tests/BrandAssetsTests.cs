using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TrainArena.Tests;

public class BrandAssetsTests : IClassFixture<TrainArenaWebAppFactory>
{
    private readonly HttpClient _client;

    public BrandAssetsTests(TrainArenaWebAppFactory factory) => _client = factory.CreateClient();

    [Fact]
    public void LogoIconSvg_ExistsOnDisk()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "TrainArena", "wwwroot", "assets", "brand", "logo-icon.svg"));
        Assert.True(File.Exists(path), $"Missing {path}");
        var svg = File.ReadAllText(path);
        Assert.Contains("TrainArena", svg);
        Assert.Contains("#0032C3", svg);
        Assert.Contains("#0ACDDE", svg);
    }

    [Fact]
    public async Task LogoIconSvg_IsServed()
    {
        var res = await _client.GetAsync("/assets/brand/logo-icon.svg");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("svg", res.Content.Headers.ContentType?.MediaType ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
