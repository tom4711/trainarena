namespace TrainArena.Tests;

public class SmokeTests
{
    [Fact]
    public void Solution_TargetsNet10()
    {
        var tfm = typeof(TrainArena.Hubs.GameHub).Assembly
            .GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false)
            .OfType<System.Runtime.Versioning.TargetFrameworkAttribute>()
            .Single()
            .FrameworkName;

        Assert.Contains("Version=v10.0", tfm);
    }
}
