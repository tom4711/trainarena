using TrainArena.Data;

namespace TrainArena.Tests;

public class QuizRulesTests
{
    [Fact]
    public void ValidateQuestion_RejectsEmptyText()
    {
        var error = QuizRules.ValidateQuestion("", "a", "b", "c", "d", 0);
        Assert.Equal("Fragetext darf nicht leer sein.", error);
    }

    [Fact]
    public void ValidateQuestion_RejectsMissingOption()
    {
        var error = QuizRules.ValidateQuestion("Q?", "a", "b", "", "d", 0);
        Assert.Equal("Alle vier Antwortoptionen sind Pflicht.", error);
    }

    [Fact]
    public void ValidateQuestion_RejectsInvalidCorrectIndex()
    {
        var error = QuizRules.ValidateQuestion("Q?", "a", "b", "c", "d", 4);
        Assert.Equal("Genau eine richtige Antwort (Index 0–3) wählen.", error);
    }

    [Fact]
    public void ValidateQuestion_AcceptsValidMc()
    {
        Assert.Null(QuizRules.ValidateQuestion("Q?", "a", "b", "c", "d", 2));
    }
}
