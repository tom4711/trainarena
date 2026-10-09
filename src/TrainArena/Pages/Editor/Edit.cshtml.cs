using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainArena.Data;
using TrainArena.Data.Entities;

namespace TrainArena.Pages.Editor;

public sealed class EditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly QuestionImageStore _images;

    public EditModel(AppDbContext db, QuestionImageStore images)
    {
        _db = db;
        _images = images;
    }

    public Quiz? Quiz { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? InfoMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Quiz = await LoadAsync(id);
        return Quiz is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostSaveTitleAsync(Guid id, string title)
    {
        var quiz = await _db.Quizzes.FindAsync(id);
        if (quiz is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            ErrorMessage = "Titel ist Pflicht.";
            Quiz = await LoadAsync(id);
            return Page();
        }

        quiz.Title = title.Trim();
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAddQuestionAsync(
        Guid id,
        string text,
        string? displayKind,
        string? option0,
        string? option1,
        string? option2,
        string? option3,
        string? option4,
        string? option5,
        int correctIndex,
        int timeLimitSeconds = 20,
        IFormFile? image = null)
    {
        var quiz = await _db.Quizzes.FirstOrDefaultAsync(q => q.Id == id);
        if (quiz is null)
        {
            return NotFound();
        }

        var kind = string.Equals(displayKind, "TrueFalse", StringComparison.OrdinalIgnoreCase)
            ? QuestionDisplayKind.TrueFalse
            : QuestionDisplayKind.Mc;

        if (kind == QuestionDisplayKind.TrueFalse)
        {
            option0 = "Wahr";
            option1 = "Falsch";
            option2 = option3 = option4 = option5 = "";
        }

        var options = new List<string>();
        foreach (var opt in new[] { option0, option1, option2, option3, option4, option5 })
        {
            if (string.IsNullOrWhiteSpace(opt))
            {
                break;
            }

            options.Add(opt.Trim());
        }

        var error = QuizRules.ValidateQuestion(text, options, correctIndex, kind);
        if (error is not null)
        {
            ErrorMessage = error;
            Quiz = await LoadAsync(id);
            return Page();
        }

        string? imagePath = null;
        if (image is { Length: > 0 })
        {
            try
            {
                await using var stream = image.OpenReadStream();
                imagePath = await _images.SaveAsync(stream, image.FileName, image.ContentType);
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
                Quiz = await LoadAsync(id);
                return Page();
            }
        }

        var maxSort = await _db.Questions
            .Where(q => q.QuizId == id)
            .MaxAsync(q => (int?)q.SortOrder);
        var question = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = id,
            Text = text.Trim(),
            DisplayKind = kind,
            CorrectIndex = correctIndex,
            TimeLimitSeconds = timeLimitSeconds <= 0 ? 20 : timeLimitSeconds,
            SortOrder = maxSort is null ? 0 : maxSort.Value + 1,
            ImagePath = imagePath
        };
        QuizRules.ApplyOptions(question, options);
        _db.Questions.Add(question);
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteQuestionAsync(Guid id, Guid questionId)
    {
        var question = await _db.Questions.FirstOrDefaultAsync(q => q.Id == questionId && q.QuizId == id);
        if (question is not null)
        {
            _images.TryDelete(question.ImagePath);
            _db.Questions.Remove(question);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostImportAsync(Guid id, IFormFile? file)
    {
        var quiz = await _db.Quizzes.Include(q => q.Questions).FirstOrDefaultAsync(q => q.Id == id);
        if (quiz is null)
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            ErrorMessage = "Bitte eine CSV-Datei wählen.";
            Quiz = quiz;
            return Page();
        }

        string csv;
        using (var reader = new StreamReader(file.OpenReadStream()))
        {
            csv = await reader.ReadToEndAsync();
        }

        var parsed = QuizImport.Parse(csv);
        if (parsed.Questions.Count == 0)
        {
            ErrorMessage = parsed.Errors.Count > 0
                ? string.Join(" ", parsed.Errors)
                : "Keine Fragen importiert.";
            Quiz = quiz;
            return Page();
        }

        var sort = quiz.Questions.Count == 0 ? 0 : quiz.Questions.Max(q => q.SortOrder) + 1;
        foreach (var q in parsed.Questions)
        {
            quiz.Questions.Add(QuizImport.ToEntity(q, id, sort++));
        }

        await _db.SaveChangesAsync();
        InfoMessage = $"{parsed.Questions.Count} Fragen importiert."
            + (parsed.Errors.Count > 0 ? $" ({parsed.Errors.Count} Zeilen übersprungen.)" : "");
        Quiz = await LoadAsync(id);
        return Page();
    }

    private Task<Quiz?> LoadAsync(Guid id) =>
        _db.Quizzes
            .AsNoTracking()
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == id);
}
