using System.Text;
using TrainArena.Data;
using TrainArena.Data.Entities;

namespace TrainArena.Tests;

public class QuizJsonImportTests
{
    [Fact]
    public void ParseJson_MapsMcAndTrueFalse()
    {
        var json = """
            {
              "title": "Demo",
              "questions": [
                {
                  "text": "Was ist 1+1?",
                  "displayKind": "mc",
                  "options": ["1", "2", "3", "4"],
                  "correctIndex": 1,
                  "timeLimitSeconds": 15
                },
                {
                  "text": "C# ist statisch typisiert.",
                  "displayKind": "trueFalse",
                  "correctIndex": 0
                }
              ]
            }
            """;

        var result = QuizJsonImport.Parse(json);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Questions.Count);
        Assert.Equal(QuestionDisplayKind.Mc, result.Questions[0].DisplayKind);
        Assert.Equal(1, result.Questions[0].CorrectIndex);
        Assert.Equal(15, result.Questions[0].TimeLimitSeconds);
        Assert.Equal(QuestionDisplayKind.TrueFalse, result.Questions[1].DisplayKind);
        Assert.Equal(["Wahr", "Falsch"], result.Questions[1].Options);
    }

    [Fact]
    public void ParseJson_AcceptsBareArrayAndCodeFenceText()
    {
        var json = """
            [
              {
                "text": "Wert von x?\n\n```csharp\nint x = 5;\n```",
                "options": ["5", "0"],
                "correctIndex": 0
              }
            ]
            """;

        var result = QuizJsonImport.Parse(json);

        Assert.Empty(result.Errors);
        Assert.Single(result.Questions);
        Assert.Contains("```csharp", result.Questions[0].Text);
    }

    [Fact]
    public void ParseJson_DecodesBase64Image()
    {
        // 1x1 PNG
        const string pngBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
        var json = $$"""
            {
              "questions": [
                {
                  "text": "Mit Bild?",
                  "options": ["Ja", "Nein", "Vielleicht", "Weiß nicht"],
                  "correctIndex": 0,
                  "image": {
                    "fileName": "dot.png",
                    "contentType": "image/png",
                    "base64": "{{pngBase64}}"
                  }
                }
              ]
            }
            """;

        var result = QuizJsonImport.Parse(json);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Questions[0].Image);
        Assert.Equal("image/png", result.Questions[0].Image!.ContentType);
        Assert.True(result.Questions[0].Image.Bytes.Length > 0);
    }

    [Fact]
    public void ParseFile_RoutesJsonByExtension()
    {
        var json = """{"questions":[{"text":"Q?","options":["a","b"],"correctIndex":0}]}""";
        var result = QuizImport.ParseFile("pack.json", json);
        Assert.Empty(result.Errors);
        Assert.Single(result.Questions);
    }

    [Fact]
    public void ParseJson_SampleCsharpPack_Succeeds()
    {
        var path = FindRepoFile("docs/quizzes/csharp-grundlagen-programmierung.json");
        Assert.True(File.Exists(path), path);

        var result = QuizJsonImport.Parse(File.ReadAllText(path, Encoding.UTF8));

        Assert.Empty(result.Errors);
        Assert.True(result.Questions.Count >= 16);
        Assert.Contains(result.Questions, q => q.Text.Contains("```csharp"));
        Assert.Contains(result.Questions, q => q.DisplayKind == QuestionDisplayKind.TrueFalse);
    }

    private static string FindRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));
    }
}
