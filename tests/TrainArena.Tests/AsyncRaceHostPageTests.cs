namespace TrainArena.Tests;

public class AsyncRaceHostPageTests
{
    [Fact]
    public async Task HostPage_HasAsyncRaceCheckbox()
    {
        await using var factory = new TrainArenaWebAppFactory();
        var html = await factory.CreateClient().GetStringAsync("/");
        Assert.Contains("async-race-enabled", html, StringComparison.Ordinal);
    }
}
