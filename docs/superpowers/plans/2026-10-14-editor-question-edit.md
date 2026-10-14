# Editor Question Edit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow editing existing quiz questions inline on the Editor Edit page (same fields as add), including image replace/remove and MC↔TrueFalse type change.

**Architecture:** Extend `EditModel` with `EditingQuestionId` (from `?edit=`), `OnPostUpdateQuestionAsync` mirroring `OnPostAddQuestionAsync` validation/option logic, and swap the question card UI for a prefilled form when editing. No new pages or services.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core SQLite, existing `QuizRules` / `QuestionImageStore`, xUnit + `TrainArenaWebAppFactory`.

**Spec:** `docs/superpowers/specs/2026-10-14-editor-question-edit-design.md`

## Global Constraints

- Inline edit in the question list only — no separate question page or modal.
- Image: optional new upload replaces old; `removeImage` clears without replacement.
- DisplayKind switch MC ↔ TrueFalse allowed (same rules as Add).
- `SortOrder` must not change on update.
- Only one question in edit mode at a time (`?edit={questionId}`).
- Branch: `cursor/editor-question-edit-2102` (base `main`).

---

## File map

| File | Role |
|------|------|
| `src/TrainArena/Pages/Editor/Edit.cshtml.cs` | `EditingQuestionId`, `OnGet` `edit` query, `OnPostUpdateQuestionAsync` |
| `src/TrainArena/Pages/Editor/Edit.cshtml` | Bearbeiten button, edit form in card, cancel link; shared MC/TF JS |
| `tests/TrainArena.Tests/QuizPersistenceTests.cs` | Update text/options + remove image (+ optional type switch) |

---

### Task 1: UpdateQuestion handler + persistence tests

**Files:**
- Modify: `src/TrainArena/Pages/Editor/Edit.cshtml.cs`
- Test: `tests/TrainArena.Tests/QuizPersistenceTests.cs`

**Interfaces:**
- Consumes: `QuizRules.CollectOptions` / `ValidateQuestion` / `ApplyOptions`, `QuestionImageStore.SaveAsync` / `TryDelete`, existing AddQuestion patterns
- Produces:
  - `public Guid? EditingQuestionId { get; private set; }`
  - `OnGetAsync(Guid id, Guid? edit = null)` sets `EditingQuestionId` when the question belongs to the quiz
  - `OnPostUpdateQuestionAsync(Guid id, Guid questionId, string text, string? displayKind, string? option0…option5, int correctIndex, int timeLimitSeconds = 20, string? explanation = null, string? tags = null, int points = 0, IFormFile? image = null, bool removeImage = false)`

- [ ] **Step 1: Write failing tests** in `QuizPersistenceTests.cs`

```csharp
[Fact]
public async Task UpdateQuestion_ChangesTextAndOptions()
{
    using var factory = new TrainArenaWebAppFactory();
    var client = factory.CreateClient(new() { AllowAutoRedirect = false });

    Guid quizId;
    Guid questionId;
    using (var scope = factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        quizId = Guid.NewGuid();
        questionId = Guid.NewGuid();
        db.Quizzes.Add(new Quiz { Id = quizId, Title = "Edit Me" });
        db.Questions.Add(new Question
        {
            Id = questionId,
            QuizId = quizId,
            Text = "Alt?",
            Option0 = "A",
            Option1 = "B",
            CorrectIndex = 0,
            TimeLimitSeconds = 20,
            SortOrder = 0,
            DisplayKind = QuestionDisplayKind.Mc
        });
        await db.SaveChangesAsync();
    }

    var pageUrl = $"/Editor/Edit/{quizId}";
    var pageHtml = await client.GetStringAsync(pageUrl);
    var tokenMatch = Regex.Match(
        pageHtml,
        @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
    Assert.True(tokenMatch.Success);

    using var form = new MultipartFormDataContent
    {
        { new StringContent(quizId.ToString()), "id" },
        { new StringContent(questionId.ToString()), "questionId" },
        { new StringContent("Neu?"), "text" },
        { new StringContent("Mc"), "displayKind" },
        { new StringContent("X"), "option0" },
        { new StringContent("Y"), "option1" },
        { new StringContent("1"), "correctIndex" },
        { new StringContent("25"), "timeLimitSeconds" },
        { new StringContent(tokenMatch.Groups[1].Value), "__RequestVerificationToken" }
    };

    var post = await client.PostAsync($"{pageUrl}?handler=UpdateQuestion", form);
    Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

    using (var scope = factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var q = await db.Questions.AsNoTracking().SingleAsync(x => x.Id == questionId);
        Assert.Equal("Neu?", q.Text);
        Assert.Equal("X", q.Option0);
        Assert.Equal("Y", q.Option1);
        Assert.Equal(1, q.CorrectIndex);
        Assert.Equal(25, q.TimeLimitSeconds);
        Assert.Equal(0, q.SortOrder);
    }
}

[Fact]
public async Task UpdateQuestion_RemoveImage_ClearsPath()
{
    using var factory = new TrainArenaWebAppFactory();
    var client = factory.CreateClient(new() { AllowAutoRedirect = false });

    Guid quizId;
    Guid questionId;
    using (var scope = factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        quizId = Guid.NewGuid();
        questionId = Guid.NewGuid();
        db.Quizzes.Add(new Quiz { Id = quizId, Title = "Img Edit" });
        db.Questions.Add(new Question
        {
            Id = questionId,
            QuizId = quizId,
            Text = "Mit Bild?",
            Option0 = "A",
            Option1 = "B",
            CorrectIndex = 0,
            TimeLimitSeconds = 20,
            SortOrder = 0,
            ImagePath = "/uploads/fake-old.png"
        });
        await db.SaveChangesAsync();
    }

    var pageUrl = $"/Editor/Edit/{quizId}";
    var pageHtml = await client.GetStringAsync(pageUrl);
    var token = Regex.Match(
        pageHtml,
        @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""").Groups[1].Value;

    using var form = new MultipartFormDataContent
    {
        { new StringContent(quizId.ToString()), "id" },
        { new StringContent(questionId.ToString()), "questionId" },
        { new StringContent("Mit Bild?"), "text" },
        { new StringContent("Mc"), "displayKind" },
        { new StringContent("A"), "option0" },
        { new StringContent("B"), "option1" },
        { new StringContent("0"), "correctIndex" },
        { new StringContent("20"), "timeLimitSeconds" },
        { new StringContent("true"), "removeImage" },
        { new StringContent(token), "__RequestVerificationToken" }
    };

    var post = await client.PostAsync($"{pageUrl}?handler=UpdateQuestion", form);
    Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

    using (var scope = factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var q = await db.Questions.AsNoTracking().SingleAsync(x => x.Id == questionId);
        Assert.Null(q.ImagePath);
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL** (handler missing)

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj \
  --filter "FullyQualifiedName~UpdateQuestion_" --nologo
```

Expected: FAIL (404 or no handler / redirect not Redirect)

- [ ] **Step 3: Implement handler in `Edit.cshtml.cs`**

Add property and extend OnGet:

```csharp
public Guid? EditingQuestionId { get; private set; }

public async Task<IActionResult> OnGetAsync(Guid id, Guid? edit = null)
{
    Quiz = await LoadAsync(id);
    if (Quiz is null)
    {
        return NotFound();
    }

    if (edit is Guid editId && Quiz.Questions.Any(q => q.Id == editId))
    {
        EditingQuestionId = editId;
    }

    return Page();
}
```

Add `OnPostUpdateQuestionAsync` — copy AddQuestion’s kind/options/validation/image block, then:

```csharp
var question = await _db.Questions.FirstOrDefaultAsync(q => q.Id == questionId && q.QuizId == id);
if (question is null)
{
    return NotFound();
}

// after validation + image handling:
if (removeImage)
{
    _images.TryDelete(question.ImagePath);
    question.ImagePath = null;
}
else if (image is { Length: > 0 })
{
    // SaveAsync as in Add; on success TryDelete old path then assign new
}

question.Text = text.Trim();
question.DisplayKind = kind;
question.CorrectIndex = correctIndex;
question.TimeLimitSeconds = timeLimitSeconds <= 0 ? 20 : timeLimitSeconds;
question.Explanation = string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim();
if (question.Explanation is { Length: > 2000 })
{
    question.Explanation = question.Explanation[..2000];
}
var tagStorage = QuestionTags.NormalizeStorage(tags);
question.Tags = string.IsNullOrEmpty(tagStorage) ? null : tagStorage;
question.Points = points < 0 ? 0 : points;
QuizRules.ApplyOptions(question, options);
// do NOT touch SortOrder
await _db.SaveChangesAsync();
return RedirectToPage(new { id });
```

On validation failure: set `EditingQuestionId = questionId`, reload Quiz, return Page().

Use exact helper name from Add: `QuestionTags.NormalizeStorage(tags)`.

- [ ] **Step 4: Run tests — expect PASS**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj \
  --filter "FullyQualifiedName~UpdateQuestion_|FullyQualifiedName~AddQuestion_" --nologo
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/TrainArena/Pages/Editor/Edit.cshtml.cs tests/TrainArena.Tests/QuizPersistenceTests.cs
git commit -m "feat(editor): UpdateQuestion handler for existing questions"
```

---

### Task 2: Inline edit UI + MC/TF script

**Files:**
- Modify: `src/TrainArena/Pages/Editor/Edit.cshtml`
- Optional smoke: assert `?edit=` page contains `handler=UpdateQuestion` / `name="questionId"`

**Interfaces:**
- Consumes: `Model.EditingQuestionId`, question fields from `Quiz.Questions`
- Produces: Bearbeiten link `asp-route-edit="@q.Id"`; Update form; Abbrechen without edit query

- [ ] **Step 1: Optional failing smoke test** in `QuizPersistenceTests.cs`

```csharp
[Fact]
public async Task EditPage_WithEditQuery_ShowsUpdateForm()
{
    using var factory = new TrainArenaWebAppFactory();
    var client = factory.CreateClient();
    Guid quizId;
    Guid questionId;
    using (var scope = factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        quizId = Guid.NewGuid();
        questionId = Guid.NewGuid();
        db.Quizzes.Add(new Quiz { Id = quizId, Title = "UI" });
        db.Questions.Add(new Question
        {
            Id = questionId, QuizId = quizId, Text = "Q?",
            Option0 = "A", Option1 = "B", CorrectIndex = 0, TimeLimitSeconds = 20, SortOrder = 0
        });
        await db.SaveChangesAsync();
    }

    var html = await client.GetStringAsync($"/Editor/Edit/{quizId}?edit={questionId}");
    Assert.Contains("handler=UpdateQuestion", html, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("name=\"questionId\"", html, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("Q?", html, StringComparison.Ordinal);
    Assert.Contains("Bild entfernen", html, StringComparison.Ordinal);
}
```

- [ ] **Step 2: FAIL → implement UI**

In the `foreach` question loop:

- If `Model.EditingQuestionId != q.Id`: keep read-only card; add  
  `<a class="secondary" href="?edit=@q.Id">Bearbeiten</a>` (or `asp-page` with `asp-route-id` + `asp-route-edit`) before Delete form.
- If `Model.EditingQuestionId == q.Id`: render edit `<form method="post" asp-page-handler="UpdateQuestion" enctype="multipart/form-data" class="edit-question-form" data-edit-form>` with hidden `id`, `questionId`, prefilled fields mirroring Add form (unique element ids: prefix `edit-` e.g. `edit-displayKind`, `edit-mc-options`, …).
- Image block: if `q.ImagePath` show `<img class="q-thumb" …>` + checkbox `name="removeImage" value="true"` label „Bild entfernen“; file input for replace.
- Buttons: `Änderungen speichern`; cancel `<a href="/Editor/Edit/@quiz.Id">Abbrechen</a>`.

Generalize bottom script to initialize **all** forms with `[data-edit-form]` plus `#add-question-form` (or run setup twice for add + edit). Prefer a function `wireQuestionForm(root)` that queries `displayKind`, `.mc-option`, etc. within `root`.

- [ ] **Step 3: PASS tests**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj \
  --filter "FullyQualifiedName~QuizPersistenceTests|FullyQualifiedName~UpdateQuestion_|FullyQualifiedName~EditPage_WithEditQuery" --nologo
```

- [ ] **Step 4: Manual check** (optional): open Editor, Bearbeiten, change text, save.

- [ ] **Step 5: Commit**

```bash
git add src/TrainArena/Pages/Editor/Edit.cshtml tests/TrainArena.Tests/QuizPersistenceTests.cs
git commit -m "feat(editor): inline edit form for existing questions"
```

---

### Task 3: Type-switch test + README note + PR

**Files:**
- Modify: `tests/TrainArena.Tests/QuizPersistenceTests.cs` (MC → TrueFalse update)
- Modify: `README.md` — one line under Editor/features if there is an editor bullet; otherwise skip or add under Features
- Modify: `docs/superpowers/specs/2026-10-14-editor-question-edit-design.md` → Status Implemented

- [ ] **Step 1: Test** `UpdateQuestion_McToTrueFalse_RewritesOptions` — post `displayKind=TrueFalse`, assert Option0/1 Wahr/Falsch and DisplayKind.

- [ ] **Step 2: Full suite**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --nologo
```

Expected: all pass

- [ ] **Step 3: Docs + commit + push + PR**

```bash
git add tests/TrainArena.Tests/QuizPersistenceTests.cs README.md docs/superpowers/specs/2026-10-14-editor-question-edit-design.md
git commit -m "docs: question edit in editor; mark spec implemented"
git push -u origin HEAD
```

Open draft PR to `main`.

---

## Spec coverage checklist

| Spec item | Task |
|-----------|------|
| Inline Bearbeiten button + form | 2 |
| Same fields as Add | 2 |
| Update handler + QuizRules validation | 1 |
| Image replace / removeImage | 1, 2 |
| MC ↔ TF switch | 1 (logic), 3 (test) |
| SortOrder unchanged | 1 (assert) |
| Single edit via `?edit=` | 1 OnGet, 2 UI |
| Abbrechen | 2 |
| Tests | 1, 2, 3 |
