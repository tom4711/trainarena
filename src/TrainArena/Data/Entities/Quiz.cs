namespace TrainArena.Data.Entities;

public sealed class Quiz
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public List<Question> Questions { get; set; } = new();
}
