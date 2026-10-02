using Microsoft.EntityFrameworkCore;
using TrainArena.Data.Entities;
using TrainArena.Game;

namespace TrainArena.Data;

/// <summary>
/// Seeds „Ausbildung Basics“ and exposes the legacy single demo question.
/// </summary>
public static class SeedData
{
    public static readonly Guid AusbildungBasicsQuizId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>First question of the seed quiz — kept for Phase 0 tests.</summary>
    public static DemoQuestion DemoQuestion { get; } = new()
    {
        Text = "Wie viele Bundesländer hat Deutschland?",
        Options = ["14", "15", "16", "17"],
        CorrectIndex = 2,
        TimeLimitSeconds = 20
    };

    public static async Task EnsureSeededAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.EnsureSchemaAsync(ct);
        if (await db.Quizzes.AnyAsync(ct))
        {
            return;
        }

        var quiz = new Quiz
        {
            Id = AusbildungBasicsQuizId,
            Title = "Ausbildung Basics",
            Questions =
            [
                new Question
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111101"),
                    Text = "Wie viele Bundesländer hat Deutschland?",
                    Option0 = "14",
                    Option1 = "15",
                    Option2 = "16",
                    Option3 = "17",
                    CorrectIndex = 2,
                    TimeLimitSeconds = 20,
                    SortOrder = 0
                },
                new Question
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111102"),
                    Text = "Was bedeutet die Abkürzung „Azubi“?",
                    Option0 = "Auszubildender",
                    Option1 = "Außerordentlicher Bilanzierer",
                    Option2 = "Arbeitszeitbilanz",
                    Option3 = "Ausbildungszuschuss intern",
                    CorrectIndex = 0,
                    TimeLimitSeconds = 20,
                    SortOrder = 1
                },
                new Question
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111103"),
                    Text = "Wer schließt den Ausbildungsvertrag?",
                    Option0 = "Nur der Ausbilder",
                    Option1 = "Ausbildungsbetrieb und Auszubildende/r (ggf. gesetzliche Vertretung)",
                    Option2 = "Nur die Berufsschule",
                    Option3 = "Die Industrie- und Handelskammer allein",
                    CorrectIndex = 1,
                    TimeLimitSeconds = 25,
                    SortOrder = 2
                },
                new Question
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111104"),
                    Text = "Wofür steht „BBiG“?",
                    Option0 = "Betriebs- und Bilanzgesetz",
                    Option1 = "Berufsbildungsgesetz",
                    Option2 = "Bundesbildungsinitiative Gesetz",
                    Option3 = "Betriebsstätten-Bauordnung intern generell",
                    CorrectIndex = 1,
                    TimeLimitSeconds = 20,
                    SortOrder = 3
                }
            ]
        };

        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync(ct);
    }
}
