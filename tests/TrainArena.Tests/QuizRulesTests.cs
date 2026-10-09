using TrainArena.Data;
using TrainArena.Data.Entities;

namespace TrainArena.Tests;

public class QuizRulesTests
{
    [Fact]
    public void ValidateQuestion_RejectsEmptyText()
    {
        var error = QuizRules.ValidateQuestion("", ["a", "b", "c", "d"], 0);
        Assert.Equal("Fragetext darf nicht leer sein.", error);
    }

    [Fact]
    public void ValidateQuestion_RejectsFewerThanTwoOptions()
    {
        var error = QuizRules.ValidateQuestion("Q?", ["a"], 0);
        Assert.Equal("Mindestens zwei Antwortoptionen sind Pflicht.", error);
    }

    [Fact]
    public void ValidateQuestion_RejectsMoreThanSixOptions()
    {
        var error = QuizRules.ValidateQuestion("Q?", ["a", "b", "c", "d", "e", "f", "g"], 0);
        Assert.Equal("Maximal sechs Antwortoptionen erlaubt.", error);
    }

    [Fact]
    public void ValidateQuestion_RejectsBlankGap()
    {
        var error = QuizRules.ValidateQuestion("Q?", ["a", "b", "", "d"], 0);
        Assert.Equal("Antwortoptionen dürfen keine Lücken haben.", error);
    }

    [Fact]
    public void ValidateQuestion_RejectsInvalidCorrectIndex()
    {
        var error = QuizRules.ValidateQuestion("Q?", ["a", "b"], 2);
        Assert.Equal("Genau eine richtige Antwort innerhalb der Optionen wählen.", error);
    }

    [Fact]
    public void ValidateQuestion_AcceptsTwoToSixOptions()
    {
        Assert.Null(QuizRules.ValidateQuestion("Q?", ["a", "b"], 1));
        Assert.Null(QuizRules.ValidateQuestion("Q?", ["a", "b", "c"], 2));
        Assert.Null(QuizRules.ValidateQuestion("Q?", ["a", "b", "c", "d", "e", "f"], 5));
    }

    [Fact]
    public void ValidateQuestion_TrueFalse_RequiresExactlyTwo()
    {
        var error = QuizRules.ValidateQuestion("Q?", ["Wahr", "Falsch", "Vielleicht"], 0, QuestionDisplayKind.TrueFalse);
        Assert.Equal("Wahr/Falsch erlaubt genau zwei Optionen.", error);
        Assert.Null(QuizRules.ValidateQuestion("Q?", ["Wahr", "Falsch"], 0, QuestionDisplayKind.TrueFalse));
    }

    [Fact]
    public void GetFilledOptions_StopsAtFirstEmpty()
    {
        var q = new Question
        {
            Text = "Q?",
            Option0 = "a",
            Option1 = "b",
            Option2 = "c",
            Option3 = "",
            Option4 = "",
            Option5 = ""
        };
        Assert.Equal(["a", "b", "c"], QuizRules.GetFilledOptions(q));
    }

    [Fact]
    public void ToDemoQuestion_MapsOnlyFilledOptions()
    {
        var demo = QuizRules.ToDemoQuestion(new Question
        {
            Text = "TF?",
            Option0 = "Wahr",
            Option1 = "Falsch",
            CorrectIndex = 1,
            TimeLimitSeconds = 15,
            DisplayKind = QuestionDisplayKind.TrueFalse
        });
        Assert.Equal(["Wahr", "Falsch"], demo.Options);
        Assert.Equal(1, demo.CorrectIndex);
        Assert.Equal(15, demo.TimeLimitSeconds);
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
    public void CollectOptions_KeepsInteriorGapSoValidationFails()
    {
        var options = QuizRules.CollectOptions(["A", "B", "", "D", null, " "]);
        Assert.Equal(["A", "B", "", "D"], options);
        Assert.Equal(
            "Antwortoptionen dürfen keine Lücken haben.",
            QuizRules.ValidateQuestion("Q?", options, 0));
    }

    [Fact]
    public void CollectOptions_TrimsAndDropsTrailingEmpties()
    {
        var options = QuizRules.CollectOptions([" A ", "B", "", null, "  "]);
        Assert.Equal(["A", "B"], options);
        Assert.Null(QuizRules.ValidateQuestion("Q?", options, 1));
    }

    [Theory]
    [InlineData("Ja", "Nein")]
    [InlineData("Falsch", "Wahr")]
    [InlineData("wahr", "falsch")]
    public void ValidateQuestion_TrueFalseRequiresExactLabels(string a, string b)
    {
        var error = QuizRules.ValidateQuestion("Q?", [a, b], 0, QuestionDisplayKind.TrueFalse);
        Assert.Equal("Wahr/Falsch erfordert die Optionen Wahr und Falsch.", error);
    }

    [Fact]
    public void ValidateQuestion_TrueFalseAcceptsWahrFalsch()
    {
        Assert.Null(QuizRules.ValidateQuestion("Q?", ["Wahr", "Falsch"], 1, QuestionDisplayKind.TrueFalse));
    }
}
