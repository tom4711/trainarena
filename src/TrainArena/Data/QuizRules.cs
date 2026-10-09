using TrainArena.Data.Entities;
using TrainArena.Game;

namespace TrainArena.Data;

public static class QuizRules
{
    public const int MinOptions = 2;
    public const int MaxOptions = 6;
    public const string TrueLabel = "Wahr";
    public const string FalseLabel = "Falsch";

    /// <summary>
    /// Trims each slot and drops trailing empty slots. Interior empties are kept as "" so
    /// <see cref="ValidateQuestion"/> reports the gap instead of silently losing later options.
    /// </summary>
    public static List<string> CollectOptions(IEnumerable<string?> raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var list = raw.Select(o => o?.Trim() ?? "").ToList();
        while (list.Count > 0 && list[^1].Length == 0)
        {
            list.RemoveAt(list.Count - 1);
        }

        return list;
    }

    public static string? ValidateQuestion(
        string text,
        IReadOnlyList<string> options,
        int correctIndex,
        QuestionDisplayKind kind = QuestionDisplayKind.Mc)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Fragetext darf nicht leer sein.";
        }

        ArgumentNullException.ThrowIfNull(options);

        for (var i = 0; i < options.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(options[i]))
            {
                return "Antwortoptionen dürfen keine Lücken haben.";
            }
        }

        if (options.Count < MinOptions)
        {
            return "Mindestens zwei Antwortoptionen sind Pflicht.";
        }

        if (options.Count > MaxOptions)
        {
            return "Maximal sechs Antwortoptionen erlaubt.";
        }

        if (kind == QuestionDisplayKind.TrueFalse && options.Count != 2)
        {
            return "Wahr/Falsch erlaubt genau zwei Optionen.";
        }

        if (kind == QuestionDisplayKind.TrueFalse
            && (options[0] != TrueLabel || options[1] != FalseLabel))
        {
            return "Wahr/Falsch erfordert die Optionen Wahr und Falsch.";
        }

        if (correctIndex < 0 || correctIndex >= options.Count)
        {
            return "Genau eine richtige Antwort innerhalb der Optionen wählen.";
        }

        return null;
    }

    public static IReadOnlyList<string> GetFilledOptions(Question q)
    {
        ArgumentNullException.ThrowIfNull(q);
        string[] all = [q.Option0, q.Option1, q.Option2, q.Option3, q.Option4, q.Option5];
        var list = new List<string>(MaxOptions);
        foreach (var opt in all)
        {
            if (string.IsNullOrWhiteSpace(opt))
            {
                break;
            }

            list.Add(opt.Trim());
        }

        return list;
    }

    public static void ApplyOptions(Question q, IReadOnlyList<string> options)
    {
        ArgumentNullException.ThrowIfNull(q);
        ArgumentNullException.ThrowIfNull(options);
        string[] slots = ["", "", "", "", "", ""];
        for (var i = 0; i < options.Count && i < MaxOptions; i++)
        {
            slots[i] = options[i].Trim();
        }

        q.Option0 = slots[0];
        q.Option1 = slots[1];
        q.Option2 = slots[2];
        q.Option3 = slots[3];
        q.Option4 = slots[4];
        q.Option5 = slots[5];
    }

    public static DemoQuestion ToDemoQuestion(Question q) => new()
    {
        Text = q.Text,
        Options = GetFilledOptions(q).ToArray(),
        CorrectIndex = q.CorrectIndex,
        TimeLimitSeconds = q.TimeLimitSeconds <= 0 ? 20 : q.TimeLimitSeconds,
        ImageUrl = string.IsNullOrWhiteSpace(q.ImagePath) ? null : q.ImagePath
    };
}
