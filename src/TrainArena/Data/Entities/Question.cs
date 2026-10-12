namespace TrainArena.Data.Entities;

public sealed class Question
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public Quiz? Quiz { get; set; }
    public string Text { get; set; } = "";
    public string Option0 { get; set; } = "";
    public string Option1 { get; set; } = "";
    public string Option2 { get; set; } = "";
    public string Option3 { get; set; } = "";
    public string Option4 { get; set; } = "";
    public string Option5 { get; set; } = "";
    public QuestionDisplayKind DisplayKind { get; set; } = QuestionDisplayKind.Mc;
    public int CorrectIndex { get; set; }
    public int TimeLimitSeconds { get; set; } = 20;
    public int SortOrder { get; set; }

    /// <summary>Public URL path under /uploads/… when set.</summary>
    public string? ImagePath { get; set; }

    /// <summary>Optional explanation shown after reveal (or in exam review).</summary>
    public string? Explanation { get; set; }

    /// <summary>Comma-separated topic tags (normalized lowercase).</summary>
    public string? Tags { get; set; }

    /// <summary>IHK weight for exam mode; 0 = equal share among questions.</summary>
    public int Points { get; set; }
}
