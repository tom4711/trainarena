using TrainArena.Game;

namespace TrainArena.Tests;

public class RoomCodeTests
{
    [Fact]
    public void Next_ReturnsSixUppercaseAlphanumeric()
    {
        var code = new RoomCodeGenerator().Next();
        Assert.Equal(6, code.Length);
        Assert.Matches("^[A-Z0-9]{6}$", code);
    }

    [Fact]
    public void Next_ProducesDistinctCodesAcrossCalls()
    {
        var generator = new RoomCodeGenerator();
        var codes = Enumerable.Range(0, 50).Select(_ => generator.Next()).ToHashSet();
        Assert.True(codes.Count > 1);
    }
}
