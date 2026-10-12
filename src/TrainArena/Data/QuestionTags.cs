namespace TrainArena.Data;

public static class QuestionTags
{
    public static string NormalizeStorage(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return "";
        }

        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tags)
        {
            var n = NormalizeOne(t);
            if (n.Length > 0)
            {
                set.Add(n);
            }
        }

        return string.Join(',', set);
    }

    public static string NormalizeStorage(string? raw) =>
        NormalizeStorage(Parse(raw));

    public static IReadOnlyList<string> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeOne)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool MatchesAny(string? stored, IReadOnlyList<string> filter)
    {
        if (filter.Count == 0)
        {
            return true;
        }

        var have = Parse(stored);
        if (have.Count == 0)
        {
            return false;
        }

        foreach (var f in filter)
        {
            var n = NormalizeOne(f);
            if (n.Length > 0 && have.Any(h => string.Equals(h, n, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeOne(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return "";
        }

        var t = tag.Trim().ToLowerInvariant();
        if (t.Length > 40)
        {
            t = t[..40];
        }

        return t;
    }
}
