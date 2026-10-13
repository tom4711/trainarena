namespace TrainArena.Game;

/// <summary>
/// IHK-style 100-point scale and grade key (Bestehen ab 50 / Note 4).
/// </summary>
public static class IhkGrading
{
    public const int MaxPoints = 100;
    public const int PassThreshold = 50;

    public static int GradeFromPoints(int points)
    {
        var p = Math.Clamp(points, 0, MaxPoints);
        return p switch
        {
            >= 92 => 1,
            >= 81 => 2,
            >= 67 => 3,
            >= 50 => 4,
            >= 30 => 5,
            _ => 6,
        };
    }

    public static bool IsPassed(int points) => points >= PassThreshold;

    public static bool IsPassedGrade(int grade) => grade is >= 1 and <= 4;

    /// <summary>
    /// Distribute <see cref="MaxPoints"/> across weights. Zero/negative weights become equal share.
    /// </summary>
    public static int[] NormalizeToHundred(IReadOnlyList<int> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var n = weights.Count;
        if (n == 0)
        {
            return [];
        }

        var effective = new double[n];
        var sum = 0.0;
        for (var i = 0; i < n; i++)
        {
            effective[i] = weights[i] > 0 ? weights[i] : 1;
            sum += effective[i];
        }

        var raw = new double[n];
        for (var i = 0; i < n; i++)
        {
            raw[i] = MaxPoints * (effective[i] / sum);
        }

        // Largest remainder method → integers sum to 100.
        var floors = new int[n];
        var frac = new (int Index, double Frac)[n];
        var used = 0;
        for (var i = 0; i < n; i++)
        {
            floors[i] = (int)Math.Floor(raw[i]);
            used += floors[i];
            frac[i] = (i, raw[i] - floors[i]);
        }

        var remaining = MaxPoints - used;
        Array.Sort(frac, (a, b) => b.Frac.CompareTo(a.Frac));
        for (var k = 0; k < remaining; k++)
        {
            floors[frac[k % n].Index]++;
        }

        return floors;
    }
}
