using TrainArena.Game;

namespace TrainArena.Data;

/// <summary>
/// Hardcoded skeleton demo content (SQLite seed comes in Phase 1).
/// </summary>
public static class SeedData
{
    public static DemoQuestion DemoQuestion { get; } = new()
    {
        Text = "Wie viele Bundesländer hat Deutschland?",
        Options =
        [
            "14",
            "15",
            "16",
            "17"
        ],
        CorrectIndex = 2,
        TimeLimitSeconds = 20
    };
}
