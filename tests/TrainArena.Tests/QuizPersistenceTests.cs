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

    private static AppDbContext CreateDb(string path)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new AppDbContext(options);
    }
}
