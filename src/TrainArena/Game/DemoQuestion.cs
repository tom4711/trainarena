namespace TrainArena.Game;

public sealed class DemoQuestion
{
    public required string Text { get; init; }
    public required string[] Options { get; init; }
    public required int CorrectIndex { get; init; }
    public int TimeLimitSeconds { get; init; } = 20;
    public string? ImageUrl { get; init; }
}

public enum GamePhase
{
    Lobby,
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
