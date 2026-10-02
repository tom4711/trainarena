using Microsoft.EntityFrameworkCore;
using TrainArena.Data;

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

    private static AppDbContext CreateDb(string path)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new AppDbContext(options);
    }
}
