using System.Globalization;
using System.Text;
using TrainArena.Data.Entities;

namespace TrainArena.Data;

public sealed record QuizImportResult(
    IReadOnlyList<ImportedQuestion> Questions,
    IReadOnlyList<string> Errors);

public sealed record ImportedQuestion(
    string Text,
    string Option0,
    string Option1,
    string Option2,
    string Option3,
    int CorrectIndex,
    int TimeLimitSeconds);

/// <summary>
/// Parses TrainArena CSV or Kahoot-like spreadsheet CSV into MC questions.
/// </summary>
public static class QuizImport
{
    public static QuizImportResult Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        csv = csv.TrimStart('\uFEFF');

        var lines = SplitLines(csv);
        var questions = new List<ImportedQuestion>();
        var errors = new List<string>();

        if (lines.Count == 0)
        {
            errors.Add("Datei ist leer.");
            return new QuizImportResult(questions, errors);
        }

        var header = ParseCsvLine(lines[0]);
        var map = BuildColumnMap(header);
        if (map is null)
        {
            errors.Add("Unbekannte CSV-Spalten. Erwartet TrainArena- oder Kahoot-ähnliche Header.");
            return new QuizImportResult(questions, errors);
        }

        for (var i = 1; i < lines.Count; i++)
        {
            var rowNumber = i + 1;
            var cells = ParseCsvLine(lines[i]);
            if (cells.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var text = Get(cells, map.Question);
            var o0 = Get(cells, map.Option0);
            var o1 = Get(cells, map.Option1);
            var o2 = Get(cells, map.Option2);
            var o3 = Get(cells, map.Option3);
            var correctRaw = Get(cells, map.Correct);
            var timeRaw = map.TimeLimit is int tIdx ? Get(cells, tIdx) : "";

            if (string.IsNullOrWhiteSpace(text)
                || string.IsNullOrWhiteSpace(o0)
                || string.IsNullOrWhiteSpace(o1)
                || string.IsNullOrWhiteSpace(o2)
                || string.IsNullOrWhiteSpace(o3))
            {
                errors.Add($"Zeile {rowNumber}: ungültig oder unvollständig — übersprungen.");
                continue;
            }

            if (!TryParseCorrect(correctRaw, map.CorrectIsOneBased, out var correctIndex))
            {
                errors.Add($"Zeile {rowNumber}: korrekte Antwort ungültig — übersprungen.");
                continue;
            }

            var validation = QuizRules.ValidateQuestion(text, o0, o1, o2, o3, correctIndex);
            if (validation is not null)
            {
                errors.Add($"Zeile {rowNumber}: {validation} — übersprungen.");
                continue;
            }

            var seconds = 20;
            if (!string.IsNullOrWhiteSpace(timeRaw)
                && int.TryParse(timeRaw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                && parsed is >= 5 and <= 120)
            {
                seconds = parsed;
            }

            questions.Add(new ImportedQuestion(
                text.Trim(),
                o0.Trim(),
                o1.Trim(),
                o2.Trim(),
                o3.Trim(),
                correctIndex,
                seconds));
        }

        if (questions.Count == 0 && errors.Count == 0)
        {
            errors.Add("Keine Fragen gefunden.");
        }

        return new QuizImportResult(questions, errors);
    }

    public static Question ToEntity(ImportedQuestion q, Guid quizId, int sortOrder) => new()
    {
        Id = Guid.NewGuid(),
        QuizId = quizId,
        Text = q.Text,
        Option0 = q.Option0,
        Option1 = q.Option1,
        Option2 = q.Option2,
        Option3 = q.Option3,
        CorrectIndex = q.CorrectIndex,
        TimeLimitSeconds = q.TimeLimitSeconds,
        SortOrder = sortOrder
    };

    private sealed record ColumnMap(
        int Question,
        int Option0,
        int Option1,
        int Option2,
        int Option3,
        int Correct,
        int? TimeLimit,
        bool CorrectIsOneBased);

    private static ColumnMap? BuildColumnMap(IReadOnlyList<string> header)
    {
        var norm = header.Select(NormalizeHeader).ToList();

        // TrainArena: Question, OptionA..D, Correct, TimeLimitSeconds?
        var q = IndexOfAny(norm, "question", "frage", "fragetext");
        var a = IndexOfAny(norm, "optiona", "option0", "a");
        var b = IndexOfAny(norm, "optionb", "option1", "b");
        var c = IndexOfAny(norm, "optionc", "option2", "c");
        var d = IndexOfAny(norm, "optiond", "option3", "d");
        var correct = IndexOfAny(norm, "correct", "correctindex", "richtig", "richtigeantwort");
        var time = IndexOfAny(norm, "timelimitseconds", "timelimit", "zeit", "zeitlimit");

        if (q >= 0 && a >= 0 && b >= 0 && c >= 0 && d >= 0 && correct >= 0)
        {
            return new ColumnMap(q, a, b, c, d, correct, time >= 0 ? time : null, CorrectIsOneBased: false);
        }

        // Kahoot-like: Question, Answer 1..4, Correct answer(s), Time limit
        q = IndexOfAny(norm, "question");
        a = IndexOfAny(norm, "answer1");
        b = IndexOfAny(norm, "answer2");
        c = IndexOfAny(norm, "answer3");
        d = IndexOfAny(norm, "answer4");
        correct = IndexOfAny(norm, "correctanswers", "correctanswer");
        time = IndexOfAny(norm, "timelimit");

        if (q >= 0 && a >= 0 && b >= 0 && c >= 0 && d >= 0 && correct >= 0)
        {
            return new ColumnMap(q, a, b, c, d, correct, time >= 0 ? time : null, CorrectIsOneBased: true);
        }

        return null;
    }

    private static string NormalizeHeader(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static int IndexOfAny(IReadOnlyList<string> headers, params string[] names)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            foreach (var name in names)
            {
                if (headers[i] == name)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static string Get(IReadOnlyList<string> cells, int index) =>
        index >= 0 && index < cells.Count ? cells[index] : "";

    private static bool TryParseCorrect(string raw, bool oneBased, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        raw = raw.Trim();

        // Take first token if Kahoot lists multiple: "1" or "1;2"
        var token = raw.Split([';', ',', '|', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? raw;

        if (token.Length == 1 && char.IsLetter(token[0]))
        {
            var letter = char.ToUpperInvariant(token[0]);
            if (letter is >= 'A' and <= 'D')
            {
                index = letter - 'A';
                return true;
            }
        }

        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            return false;
        }

        if (oneBased)
        {
            if (n is >= 1 and <= 4)
            {
                index = n - 1;
                return true;
            }

            return false;
        }

        // TrainArena: prefer 0–3; also accept 1–4 as convenience
        if (n is >= 0 and <= 3)
        {
            index = n;
            return true;
        }

        if (n is >= 1 and <= 4)
        {
            index = n - 1;
            return true;
        }

        return false;
    }

    private static List<string> SplitLines(string csv)
    {
        var lines = new List<string>();
        using var reader = new StringReader(csv);
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }

    /// <summary>Minimal CSV line parser (quotes + commas).</summary>
    internal static List<string> ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                cells.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }

        cells.Add(sb.ToString());
        return cells;
    }
}
