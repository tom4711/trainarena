namespace TrainArena.Game;

/// <summary>
/// Server-authoritative scoring: base points + speed bonus from receive time (Q7=A).
/// </summary>
public static class Scoring
{
    public const int BasePoints = 500;
    public const int MaxSpeedBonus = 500;

    public static int Score(bool correct, TimeSpan elapsed, TimeSpan limit)
    {
        if (!correct)
        {
            return 0;
        }

        if (limit <= TimeSpan.Zero)
        {
            return BasePoints;
        }

        var clampedElapsed = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        if (clampedElapsed >= limit)
        {
            return BasePoints;
        }

        var remainingRatio = 1.0 - clampedElapsed.TotalMilliseconds / limit.TotalMilliseconds;
        var bonus = (int)Math.Round(MaxSpeedBonus * remainingRatio);
        return BasePoints + bonus;
    }
}
