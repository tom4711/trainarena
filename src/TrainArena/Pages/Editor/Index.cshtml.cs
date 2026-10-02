using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainArena.Data;
using TrainArena.Data.Entities;

namespace TrainArena.Pages.Editor;

public sealed class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    public IList<Quiz> Quizzes { get; private set; } = new List<Quiz>();

    public async Task OnGetAsync()
    {
        Quizzes = await _db.Quizzes
            .AsNoTracking()
            .Include(q => q.Questions)
            .OrderBy(q => q.Title)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            ModelState.AddModelError(string.Empty, "Titel ist Pflicht.");
            await OnGetAsync();
            return Page();
        }

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = title.Trim()
        };
        _db.Quizzes.Add(quiz);
        await _db.SaveChangesAsync();
        return RedirectToPage("Edit", new { id = quiz.Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var quiz = await _db.Quizzes.FindAsync(id);
        if (quiz is not null)
        {
            _db.Quizzes.Remove(quiz);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage();
    }
}
