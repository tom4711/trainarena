namespace TrainArena.Game;

/// <summary>Built-in demo question for StartDemo / legacy Phase-0 tests.</summary>
public static class SeedQuestions
{
    public static QuizQuestion ClassicDemo { get; } = new()
    {
        Text = "Wie viele Bundesländer hat Deutschland?",
        Options = ["14", "15", "16", "17"],
        CorrectIndex = 2,
        TimeLimitSeconds = 20,
    };
}
