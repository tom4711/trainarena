namespace TrainArena.Game;

/// <summary>Immutable view of a player's async-race progress (safe outside the session lock).</summary>
public sealed record PlayerRaceSnapshot(
    IReadOnlyList<QuizQuestion> Order,
    int Index,
    DateTimeOffset? QuestionStartedAtUtc,
    DateTimeOffset? EndsAtUtc)
{
    public bool Finished => Index >= Order.Count;
    public QuizQuestion? Current => Finished ? null : Order[Index];
    public int AnsweredCount => Math.Min(Index, Order.Count);
}
