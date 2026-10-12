using System.Text.Json;
using TrainArena.Data.Entities;

namespace TrainArena.Data;

/// <summary>
/// JSON quiz-pack parser used by <see cref="QuizImport.ParseFile"/>.
/// </summary>
public static class QuizJsonImport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static QuizImportResult Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        json = json.TrimStart('\uFEFF').Trim();

        var questions = new List<ImportedQuestion>();
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(json))
        {
            errors.Add("Datei ist leer.");
            return new QuizImportResult(questions, errors);
        }

        QuizJsonPack? pack;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                pack = new QuizJsonPack { Questions = doc.RootElement.Deserialize<List<QuizJsonQuestion>>(JsonOptions) };
            }
            else
            {
                pack = JsonSerializer.Deserialize<QuizJsonPack>(json, JsonOptions);
            }
        }
        catch (JsonException ex)
        {
            errors.Add($"JSON ungültig: {ex.Message}");
            return new QuizImportResult(questions, errors);
        }

        if (pack?.Questions is null || pack.Questions.Count == 0)
        {
            errors.Add("Keine Fragen im JSON gefunden (Feld „questions“ oder Array).");
            return new QuizImportResult(questions, errors);
        }

        for (var i = 0; i < pack.Questions.Count; i++)
        {
            var raw = pack.Questions[i];
            var label = $"Frage {i + 1}";
            if (raw is null)
            {
                errors.Add($"{label}: leer — übersprungen.");
                continue;
            }

            var text = raw.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                errors.Add($"{label}: Fragetext fehlt — übersprungen.");
                continue;
            }

            var kind = ParseKind(raw.DisplayKind);
            List<string> options;
            if (kind == QuestionDisplayKind.TrueFalse)
            {
                options = [QuizRules.TrueLabel, QuizRules.FalseLabel];
            }
            else
            {
                options = QuizRules.CollectOptions(raw.Options ?? []);
                if (options.Any(string.IsNullOrEmpty))
                {
                    errors.Add($"{label}: Antwortoptionen dürfen keine Lücken haben — übersprungen.");
                    continue;
                }
            }

            var correctIndex = raw.CorrectIndex;
            var validation = QuizRules.ValidateQuestion(text, options, correctIndex, kind);
            if (validation is not null)
            {
                errors.Add($"{label}: {validation} — übersprungen.");
                continue;
            }

            var seconds = raw.TimeLimitSeconds is >= 5 and <= 120 ? raw.TimeLimitSeconds.Value : 20;
            ImportedImage? image = null;
            if (raw.Image is not null)
            {
                var (img, imgError) = DecodeImage(raw.Image, label);
                if (imgError is not null)
                {
                    errors.Add(imgError);
                    continue;
                }

                image = img;
            }

            var explanation = string.IsNullOrWhiteSpace(raw.Explanation) ? null : raw.Explanation.Trim();
            if (explanation is { Length: > 2000 })
            {
                explanation = explanation[..2000];
            }

            var tags = raw.Tags is { Count: > 0 }
                ? QuestionTags.Parse(QuestionTags.NormalizeStorage(raw.Tags))
                : null;
            var points = raw.Points is > 0 ? raw.Points.Value : 0;

            questions.Add(new ImportedQuestion(
                text,
                options,
                correctIndex,
                seconds,
                kind,
                image,
                explanation,
                tags,
                points));
        }

        if (questions.Count == 0 && errors.Count == 0)
        {
            errors.Add("Keine Fragen gefunden.");
        }

        return new QuizImportResult(questions, errors);
    }

    private static QuestionDisplayKind ParseKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return QuestionDisplayKind.Mc;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "truefalse" or "true_false" or "tf" or "wahrfalsch" or "wahr_falsch" => QuestionDisplayKind.TrueFalse,
            _ => QuestionDisplayKind.Mc,
        };
    }

    private static (ImportedImage? Image, string? Error) DecodeImage(QuizJsonImage raw, string label)
    {
        if (string.IsNullOrWhiteSpace(raw.Base64))
        {
            return (null, $"{label}: image.base64 fehlt — übersprungen.");
        }

        var b64 = raw.Base64.Trim();
        var contentType = raw.ContentType?.Trim() ?? "";
        if (b64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = b64.IndexOf(',');
            if (comma <= 0)
            {
                return (null, $"{label}: data-URL ungültig — übersprungen.");
            }

            var meta = b64[..comma];
            b64 = b64[(comma + 1)..];
            var mimeEnd = meta.IndexOf(';');
            if (meta.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                contentType = mimeEnd > 5 ? meta[5..mimeEnd] : meta[5..];
            }
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            contentType = GuessContentType(raw.FileName) ?? "image/png";
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(b64);
        }
        catch (FormatException)
        {
            return (null, $"{label}: image.base64 ungültig — übersprungen.");
        }

        if (bytes.Length == 0)
        {
            return (null, $"{label}: Bild ist leer — übersprungen.");
        }

        if (bytes.Length > QuestionImageStore.DefaultMaxBytes)
        {
            return (null, $"{label}: Bild zu groß (max. 2 MB) — übersprungen.");
        }

        var fileName = string.IsNullOrWhiteSpace(raw.FileName) ? "image.png" : raw.FileName.Trim();
        return (new ImportedImage(fileName, contentType, bytes), null);
    }

    private static string? GuessContentType(string? fileName)
    {
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => null,
        };
    }

    private sealed class QuizJsonPack
    {
        public string? Title { get; set; }
        public List<QuizJsonQuestion>? Questions { get; set; }
    }

    private sealed class QuizJsonQuestion
    {
        public string? Text { get; set; }
        public string? DisplayKind { get; set; }
        public List<string>? Options { get; set; }
        public int CorrectIndex { get; set; }
        public int? TimeLimitSeconds { get; set; }
        public QuizJsonImage? Image { get; set; }
        public string? Explanation { get; set; }
        public List<string>? Tags { get; set; }
        public int? Points { get; set; }
    }

    private sealed class QuizJsonImage
    {
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public string? Base64 { get; set; }
    }
}
