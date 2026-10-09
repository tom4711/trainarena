using System.Text;
using TrainArena.Data;

namespace TrainArena.Tests;

public class QuizImportTests
{
    [Fact]
    public void ParseCsv_MapsFourOptionRows()
    {
        var csv = """
            Question,OptionA,OptionB,OptionC,OptionD,Correct,TimeLimitSeconds
            Wie viele Bundesländer?,14,15,16,17,C,20
            Was ist Azubi?,Auszubildender,Bilanz,Zeit,Zuschuss,A,25
            """;

        var result = QuizImport.Parse(csv);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Questions.Count);
        Assert.Equal("Wie viele Bundesländer?", result.Questions[0].Text);
        Assert.Equal("16", result.Questions[0].Options[2]);
        Assert.Equal(2, result.Questions[0].CorrectIndex);
        Assert.Equal(20, result.Questions[0].TimeLimitSeconds);
        Assert.Equal(0, result.Questions[1].CorrectIndex);
        Assert.Equal(25, result.Questions[1].TimeLimitSeconds);
    }

    [Fact]
    public void ParseCsv_SkipsInvalidRowsAndReportsErrors()
    {
        var csv = """
            Question,OptionA,OptionB,OptionC,OptionD,Correct
            Gut,a,b,c,d,1
            ,a,b,c,d,0
            Lücke,a,,c,d,0
            """;

        var result = QuizImport.Parse(csv);

        Assert.Single(result.Questions);
        Assert.Equal("Gut", result.Questions[0].Text);
        Assert.Equal(1, result.Questions[0].CorrectIndex);
        Assert.True(result.Errors.Count >= 2);
    }

    [Fact]
    public void ParseCsv_AcceptsTwoOptionsAndOptionalEF()
    {
        var csv = """
            Question,OptionA,OptionB,OptionC,OptionD,OptionE,OptionF,Correct,TimeLimitSeconds
            Wahr?,Ja,Nein,,,,,A,15
            Sechs?,a,b,c,d,e,f,F,20
            """;

        var result = QuizImport.Parse(csv);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Questions.Count);
        Assert.Equal(["Ja", "Nein"], result.Questions[0].Options);
        Assert.Equal(0, result.Questions[0].CorrectIndex);
        Assert.Equal(15, result.Questions[0].TimeLimitSeconds);
        Assert.Equal(6, result.Questions[1].Options.Count);
        Assert.Equal(5, result.Questions[1].CorrectIndex);
    }

    [Fact]
    public void ParseKahootLikeCsv_MapsAnswerColumns()
    {
        var csv = """
            Question Number,Question,Answer 1,Answer 2,Answer 3,Answer 4,Time limit,Correct answer(s)
            1,Farbe der Ampel bei Stop?,Rot,Gelb,Grün,Blau,20,1
            2,2+2?,3,4,5,6,30,2
            """;

        var result = QuizImport.Parse(csv);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Questions.Count);
        Assert.Equal("Farbe der Ampel bei Stop?", result.Questions[0].Text);
        Assert.Equal("Rot", result.Questions[0].Options[0]);
        Assert.Equal(0, result.Questions[0].CorrectIndex);
        Assert.Equal(20, result.Questions[0].TimeLimitSeconds);
        Assert.Equal(1, result.Questions[1].CorrectIndex);
        Assert.Equal(30, result.Questions[1].TimeLimitSeconds);
    }

    [Fact]
    public void ParseKahootLikeCsv_AcceptsTwoAnswers()
    {
        var csv = """
            Question,Answer 1,Answer 2,Answer 3,Answer 4,Time limit,Correct answer(s)
            Himmel blau?,Wahr,Falsch,,,20,1
            """;

        var result = QuizImport.Parse(csv);

        Assert.Empty(result.Errors);
        Assert.Single(result.Questions);
        Assert.Equal(["Wahr", "Falsch"], result.Questions[0].Options);
        Assert.Equal(0, result.Questions[0].CorrectIndex);
    }

    [Fact]
    public void ParseCsv_AcceptsUtf8Bom()
    {
        var body = "Question,OptionA,OptionB,OptionC,OptionD,Correct\nQ?,a,b,c,d,B\n";
        var bom = Encoding.UTF8.GetPreamble();
        var bytes = bom.Concat(Encoding.UTF8.GetBytes(body)).ToArray();
        var csv = Encoding.UTF8.GetString(bytes);

        var result = QuizImport.Parse(csv);

        Assert.Single(result.Questions);
        Assert.Equal(1, result.Questions[0].CorrectIndex);
    }
}
