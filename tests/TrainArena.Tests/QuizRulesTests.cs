using TrainArena.Data;
using TrainArena.Data.Entities;

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

    [Fact]
    public void ToDemoQuestion_MapsImagePathToImageUrl()
    {
        var demo = QuizRules.ToDemoQuestion(new Question
        {
            Text = "Mit Bild?",
            Option0 = "a",
            Option1 = "b",
            Option2 = "c",
            Option3 = "d",
            CorrectIndex = 0,
            TimeLimitSeconds = 20,
            ImagePath = "/uploads/abc.png"
        });

        Assert.Equal("/uploads/abc.png", demo.ImageUrl);
    }

    [Fact]
    public void ToDemoQuestion_NullImageWhenMissing()
    {
        var demo = QuizRules.ToDemoQuestion(new Question
        {
            Text = "Ohne Bild?",
            Option0 = "a",
            Option1 = "b",
            Option2 = "c",
            Option3 = "d",
            CorrectIndex = 1
        });

        Assert.Null(demo.ImageUrl);
    }
}
