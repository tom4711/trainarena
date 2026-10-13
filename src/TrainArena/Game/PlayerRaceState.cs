namespace TrainArena.Game;

public sealed class PlayerRaceState
{
    public required IReadOnlyList<DemoQuestion> Order { get; init; }
    public int Index { get; set; } // 0..Order.Count (Count means finished)
    public DateTimeOffset? QuestionStartedAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public bool Finished => Index >= Order.Count;
    public DemoQuestion? Current => Finished ? null : Order[Index];
    public int AnsweredCount => Math.Min(Index, Order.Count);
}
