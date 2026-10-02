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
        Assert.Equal("16", result.Questions[0].Option2);
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
            Nur drei,a,b,c,,0
            """;

        var result = QuizImport.Parse(csv);

        Assert.Single(result.Questions);
        Assert.Equal("Gut", result.Questions[0].Text);
        Assert.Equal(1, result.Questions[0].CorrectIndex);
        Assert.True(result.Errors.Count >= 2);
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
        Assert.Equal("Rot", result.Questions[0].Option0);
        Assert.Equal(0, result.Questions[0].CorrectIndex);
        Assert.Equal(20, result.Questions[0].TimeLimitSeconds);
        Assert.Equal(1, result.Questions[1].CorrectIndex);
        Assert.Equal(30, result.Questions[1].TimeLimitSeconds);
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
