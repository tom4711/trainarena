using TrainArena.Data;
using TrainArena.Data.Entities;

namespace TrainArena.Game;

/// <summary>
/// Maps persisted questions into runtime <see cref="QuizQuestion"/> values.
/// Lives in Game (not Data) so the persistence layer does not own live-game types.
/// </summary>
public static class QuizQuestionMapper
{
    public static QuizQuestion FromEntity(Question q)
    {
        ArgumentNullException.ThrowIfNull(q);
        return new QuizQuestion
        {
            Text = q.Text,
            Options = QuizRules.GetFilledOptions(q).ToArray(),
            CorrectIndex = q.CorrectIndex,
            TimeLimitSeconds = q.TimeLimitSeconds <= 0 ? 20 : q.TimeLimitSeconds,
            ImageUrl = string.IsNullOrWhiteSpace(q.ImagePath) ? null : q.ImagePath,
            Explanation = string.IsNullOrWhiteSpace(q.Explanation) ? null : q.Explanation.Trim(),
            Tags = QuestionTags.Parse(q.Tags),
            Points = q.Points < 0 ? 0 : q.Points,
        };
    }
}
