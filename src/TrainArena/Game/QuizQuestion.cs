namespace TrainArena.Game;

/// <summary>Runtime question used by live rooms (mapped from persisted entities).</summary>
public sealed class QuizQuestion
{
    public required string Text { get; init; }
    public required string[] Options { get; init; }
    public required int CorrectIndex { get; init; }
    public int TimeLimitSeconds { get; init; } = 20;
    public string? ImageUrl { get; init; }
    public string? Explanation { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public int Points { get; init; }
}

public enum GamePhase
{
    Lobby,
    AsyncRacing,
    QuestionActive,
    Reveal,
    Leaderboard,
    Finished
}

public sealed class LeaderboardEntry
{
    public required string Nickname { get; init; }
    public required int Score { get; init; }
}
