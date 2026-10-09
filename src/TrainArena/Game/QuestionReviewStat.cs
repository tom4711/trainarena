namespace TrainArena.Game;

public sealed class QuestionReviewStat
{
    public required int QuestionIndex { get; init; }
    public required string Text { get; init; }
    public required string[] Options { get; init; }
    public required int CorrectIndex { get; init; }
    public required int[] Counts { get; init; }
    public required int AnsweredCount { get; init; }
    public required int PlayerCount { get; init; }
    public required int CorrectCount { get; init; }
}
