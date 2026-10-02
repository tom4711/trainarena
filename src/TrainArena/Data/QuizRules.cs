using TrainArena.Data.Entities;
using TrainArena.Game;

namespace TrainArena.Data;

/// <summary>
/// Quiz validation and mapping helpers for editor + gameplay.
/// </summary>
public static class QuizRules
{
    public static string? ValidateQuestion(
        string text,
        string option0,
        string option1,
        string option2,
        string option3,
        int correctIndex)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Fragetext darf nicht leer sein.";
        }

        if (string.IsNullOrWhiteSpace(option0)
            || string.IsNullOrWhiteSpace(option1)
            || string.IsNullOrWhiteSpace(option2)
            || string.IsNullOrWhiteSpace(option3))
        {
            return "Alle vier Antwortoptionen sind Pflicht.";
        }

        if (correctIndex is < 0 or > 3)
        {
            return "Genau eine richtige Antwort (Index 0–3) wählen.";
        }

        return null;
    }

    public static DemoQuestion ToDemoQuestion(Question q) => new()
    {
        Text = q.Text,
        Options = [q.Option0, q.Option1, q.Option2, q.Option3],
        CorrectIndex = q.CorrectIndex,
        TimeLimitSeconds = q.TimeLimitSeconds <= 0 ? 20 : q.TimeLimitSeconds,
        ImageUrl = string.IsNullOrWhiteSpace(q.ImagePath) ? null : q.ImagePath
    };
}
