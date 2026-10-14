using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainArena.Data;
using TrainArena.Data.Entities;

namespace TrainArena.Tests;

public class QuizPersistenceTests
{
    [Fact]
    public async Task EnsureSeeded_WritesAusbildungBasicsWithQuestions()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"seed-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = CreateDb(dbPath);
            await SeedData.EnsureSeededAsync(db);

            var quiz = await db.Quizzes.Include(q => q.Questions).SingleAsync();
            Assert.Equal("Ausbildung Basics", quiz.Title);
            Assert.Equal(SeedData.AusbildungBasicsQuizId, quiz.Id);
            Assert.True(quiz.Questions.Count >= 3);
            Assert.Contains(quiz.Questions, q => q.Text.Contains("Bundesländer"));
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task EnsureSeeded_IsIdempotent()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"seed-{Guid.NewGuid():N}.db");
        try
        {
            await using (var db = CreateDb(dbPath))
            {
                await SeedData.EnsureSeededAsync(db);
                await SeedData.EnsureSeededAsync(db);
                Assert.Equal(1, await db.Quizzes.CountAsync());
            }
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task AddQuestion_PersistsTwoOptionsAndTrueFalseKind()
    {
        using var factory = new TrainArenaWebAppFactory();
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var quizId = SeedData.AusbildungBasicsQuizId;
        var pageUrl = $"/Editor/Edit/{quizId}";
        var pageHtml = await client.GetStringAsync(pageUrl);
        var tokenMatch = Regex.Match(
            pageHtml,
            @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
        Assert.True(tokenMatch.Success, "Antiforgery token expected on editor page.");

        using var form = new MultipartFormDataContent
        {
            { new StringContent(quizId.ToString()), "id" },
            { new StringContent("Ist das ein Test?"), "text" },
            { new StringContent("TrueFalse"), "displayKind" },
            { new StringContent("1"), "correctIndex" },
            { new StringContent("20"), "timeLimitSeconds" },
            { new StringContent(tokenMatch.Groups[1].Value), "__RequestVerificationToken" }
        };

        var post = await client.PostAsync($"{pageUrl}?handler=AddQuestion", form);
        var postBody = await post.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var question = await db.Questions.AsNoTracking()
                .SingleAsync(q => q.QuizId == quizId && q.Text == "Ist das ein Test?");
            Assert.Equal(QuestionDisplayKind.TrueFalse, question.DisplayKind);
            var filled = QuizRules.GetFilledOptions(question);
            Assert.Equal(2, filled.Count);
            Assert.Equal("Wahr", filled[0]);
            Assert.Equal("Falsch", filled[1]);
            Assert.Equal(1, question.CorrectIndex);
        }
    }

    [Fact]
    public async Task EditPage_WithEditQuery_ShowsUpdateForm()
    {
        using var factory = new TrainArenaWebAppFactory();
        var client = factory.CreateClient();
        Guid quizId;
        Guid questionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            quizId = Guid.NewGuid();
            questionId = Guid.NewGuid();
            db.Quizzes.Add(new Quiz { Id = quizId, Title = "UI" });
            db.Questions.Add(new Question
            {
                Id = questionId,
                QuizId = quizId,
                Text = "Q?",
                Option0 = "A",
                Option1 = "B",
                CorrectIndex = 0,
                TimeLimitSeconds = 20,
                SortOrder = 0,
                ImagePath = "/uploads/smoke-thumb.png"
            });
            await db.SaveChangesAsync();
        }

        var html = await client.GetStringAsync($"/Editor/Edit/{quizId}?edit={questionId}");
        Assert.Contains("handler=UpdateQuestion", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"questionId\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Q?", html, StringComparison.Ordinal);
        Assert.Contains("Bild entfernen", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateQuestion_ChangesTextAndOptions()
    {
        using var factory = new TrainArenaWebAppFactory();
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        Guid quizId;
        Guid questionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            quizId = Guid.NewGuid();
            questionId = Guid.NewGuid();
            db.Quizzes.Add(new Quiz { Id = quizId, Title = "Edit Me" });
            db.Questions.Add(new Question
            {
                Id = questionId,
                QuizId = quizId,
                Text = "Alt?",
                Option0 = "A",
                Option1 = "B",
                CorrectIndex = 0,
                TimeLimitSeconds = 20,
                SortOrder = 0,
                DisplayKind = QuestionDisplayKind.Mc
            });
            await db.SaveChangesAsync();
        }

        var pageUrl = $"/Editor/Edit/{quizId}";
        var pageHtml = await client.GetStringAsync(pageUrl);
        var tokenMatch = Regex.Match(
            pageHtml,
            @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
        Assert.True(tokenMatch.Success);

        using var form = new MultipartFormDataContent
        {
            { new StringContent(quizId.ToString()), "id" },
            { new StringContent(questionId.ToString()), "questionId" },
            { new StringContent("Neu?"), "text" },
            { new StringContent("Mc"), "displayKind" },
            { new StringContent("X"), "option0" },
            { new StringContent("Y"), "option1" },
            { new StringContent("1"), "correctIndex" },
            { new StringContent("25"), "timeLimitSeconds" },
            { new StringContent(tokenMatch.Groups[1].Value), "__RequestVerificationToken" }
        };

        var post = await client.PostAsync($"{pageUrl}?handler=UpdateQuestion", form);
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var q = await db.Questions.AsNoTracking().SingleAsync(x => x.Id == questionId);
            Assert.Equal("Neu?", q.Text);
            Assert.Equal("X", q.Option0);
            Assert.Equal("Y", q.Option1);
            Assert.Equal(1, q.CorrectIndex);
            Assert.Equal(25, q.TimeLimitSeconds);
            Assert.Equal(0, q.SortOrder);
        }
    }

    [Fact]
    public async Task UpdateQuestion_RemoveImage_ClearsPath()
    {
        using var factory = new TrainArenaWebAppFactory();
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        Guid quizId;
        Guid questionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            quizId = Guid.NewGuid();
            questionId = Guid.NewGuid();
            db.Quizzes.Add(new Quiz { Id = quizId, Title = "Img Edit" });
            db.Questions.Add(new Question
            {
                Id = questionId,
                QuizId = quizId,
                Text = "Mit Bild?",
                Option0 = "A",
                Option1 = "B",
                CorrectIndex = 0,
                TimeLimitSeconds = 20,
                SortOrder = 0,
                ImagePath = "/uploads/fake-old.png"
            });
            await db.SaveChangesAsync();
        }

        var pageUrl = $"/Editor/Edit/{quizId}";
        var pageHtml = await client.GetStringAsync(pageUrl);
        var token = Regex.Match(
            pageHtml,
            @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""").Groups[1].Value;

        using var form = new MultipartFormDataContent
        {
            { new StringContent(quizId.ToString()), "id" },
            { new StringContent(questionId.ToString()), "questionId" },
            { new StringContent("Mit Bild?"), "text" },
            { new StringContent("Mc"), "displayKind" },
            { new StringContent("A"), "option0" },
            { new StringContent("B"), "option1" },
            { new StringContent("0"), "correctIndex" },
            { new StringContent("20"), "timeLimitSeconds" },
            { new StringContent("true"), "removeImage" },
            { new StringContent(token), "__RequestVerificationToken" }
        };

        var post = await client.PostAsync($"{pageUrl}?handler=UpdateQuestion", form);
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var q = await db.Questions.AsNoTracking().SingleAsync(x => x.Id == questionId);
            Assert.Null(q.ImagePath);
        }
    }

    [Fact]
    public async Task ImportCsv_PersistsQuestionsWithoutServerError()
    {
        using var factory = new TrainArenaWebAppFactory();
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        Guid quizId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var quiz = new Quiz { Id = Guid.NewGuid(), Title = "CSV Import Bug" };
            db.Quizzes.Add(quiz);
            await db.SaveChangesAsync();
            quizId = quiz.Id;
        }

        var pageUrl = $"/Editor/Edit/{quizId}";
        var pageHtml = await client.GetStringAsync(pageUrl);
        var tokenMatch = Regex.Match(
            pageHtml,
            @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
        Assert.True(tokenMatch.Success, "Antiforgery token expected on editor page.");

        const string csv = """
            Question,OptionA,OptionB,OptionC,OptionD,Correct,TimeLimitSeconds
            Welcher Typ speichert ganze Zahlen?,int,double,bool,string,A,20
            Was bedeutet void?,Kein Rückgabewert,Leerer string,Nur private,Immer async,A,22
            """;
        using var form = new MultipartFormDataContent
        {
            { new StringContent(quizId.ToString()), "id" },
            { new StringContent(tokenMatch.Groups[1].Value), "__RequestVerificationToken" },
            { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv))
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv") }
                },
                "file", "sample.csv" }
        };

        var post = await client.PostAsync($"{pageUrl}?handler=Import", form);
        var postBody = await post.Content.ReadAsStringAsync();
        Assert.True(
            post.IsSuccessStatusCode,
            $"Import should not 500. Status={post.StatusCode}. Body starts: {postBody[..Math.Min(300, postBody.Length)]}");
        Assert.Contains("2 Fragen importiert", postBody, StringComparison.Ordinal);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var questions = await db.Questions.AsNoTracking()
                .Where(q => q.QuizId == quizId)
                .OrderBy(q => q.SortOrder)
                .ToListAsync();
            Assert.Equal(2, questions.Count);
            Assert.Equal("Welcher Typ speichert ganze Zahlen?", questions[0].Text);
            Assert.Equal("int", questions[0].Option0);
            Assert.Equal(0, questions[0].CorrectIndex);
            Assert.Equal("Was bedeutet void?", questions[1].Text);
        }
    }

    [Fact]
    public async Task EnsureSchema_UpgradesLegacyQuestionsTable()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"legacy-{Guid.NewGuid():N}.db");
        var quizId = Guid.NewGuid();
        try
        {
            await using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"""
                    CREATE TABLE Quizzes (Id TEXT NOT NULL PRIMARY KEY, Title TEXT NOT NULL);
                    CREATE TABLE Questions (
                        Id TEXT NOT NULL PRIMARY KEY,
                        QuizId TEXT NOT NULL,
                        Text TEXT NOT NULL,
                        Option0 TEXT NOT NULL,
                        Option1 TEXT NOT NULL,
                        Option2 TEXT NOT NULL,
                        Option3 TEXT NOT NULL,
                        CorrectIndex INTEGER NOT NULL,
                        TimeLimitSeconds INTEGER NOT NULL,
                        SortOrder INTEGER NOT NULL
                    );
                    INSERT INTO Quizzes VALUES ('{quizId}', 'Legacy');
                    INSERT INTO Questions VALUES ('{Guid.NewGuid()}', '{quizId}', 'Alt?', 'a', 'b', 'c', 'd', 2, 20, 0);
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var db = CreateDb(dbPath))
            {
                await db.EnsureSchemaAsync();
            }

            await using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('Questions');";
                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    columns.Add(reader.GetString(1));
                }

                Assert.Contains("Option4", columns);
                Assert.Contains("Option5", columns);
                Assert.Contains("DisplayKind", columns);
                Assert.Contains("ImagePath", columns);
            }

            await using (var db = CreateDb(dbPath))
            {
                var q = await db.Questions.SingleAsync(x => x.Text == "Alt?");
                Assert.Equal("Alt?", q.Text);
                Assert.Equal(2, q.CorrectIndex);
                Assert.Equal("", q.Option4);
                Assert.Equal("", q.Option5);
                Assert.Equal(QuestionDisplayKind.Mc, q.DisplayKind);
                Assert.Equal(4, QuizRules.GetFilledOptions(q).Count);
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }

    private static AppDbContext CreateDb(string path)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new AppDbContext(options);
    }
}
