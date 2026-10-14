namespace TrainArena.Game;

/// <summary>Domain scoreboard row for async race (mapped to Contracts at Hub edge).</summary>
public sealed record AsyncScoreboardEntry(string Nickname, int Score, int AnsweredCount, int Total);
