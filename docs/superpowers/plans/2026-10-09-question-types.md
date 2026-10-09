# Variable Question Types Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Support Wahr/Falsch and MC with 2–6 options (single correct), without breaking existing 4-option quizzes, CSV imports, or Power-Ups.

**Architecture:** Persist `Option0`…`Option5` plus `DisplayKind` on `Question`; gameplay continues to use `DemoQuestion.Options` (`string[]`). `QuizRules` validates contiguous 2–6 options; `GameSession` accepts variable length and scales 50/50 (mask 2 / 1 / reject). Editor offers MC vs Wahr/Falsch UI; import stays backward compatible with optional E/F.

**Tech Stack:** .NET 10 · ASP.NET Core · SignalR · SQLite/EF Core · Razor Editor · vanilla Host/Player JS · xUnit

## Global Constraints

- Spec: `docs/superpowers/specs/2026-10-09-question-types-design.md`
- Exactly one correct answer; option count 2–6; no gaps in option columns
- `DisplayKind`: `Mc` | `TrueFalse` — editor/UX only; TrueFalse → fixed labels „Wahr“/„Falsch“
- 50/50: ≥4 → mask 2 wrong; =3 → mask 1; ≤2 → reject + UI disabled
- Import: old 4-column CSVs valid; optional `OptionE`/`OptionF` / Answer 5–6; import always `DisplayKind=Mc`
- Existing seed quizzes and 4-option tests stay green (except assertions that required “exactly four”)
- German UI copy; TDD; branch `cursor/question-types-design-b12d` (or continue on same feature branch)
- Frage-Review is **out of scope**

---

## File map

| File | Role |
|------|------|
| `src/TrainArena/Data/Entities/QuestionDisplayKind.cs` | Enum `Mc`, `TrueFalse` |
| `src/TrainArena/Data/Entities/Question.cs` | `Option4`, `Option5`, `DisplayKind` |
| `src/TrainArena/Data/AppDbContext.cs` | EF config + `EnsureSchemaAsync` column patches |
| `src/TrainArena/Data/QuizRules.cs` | Validate 2–6; `GetFilledOptions`; map to `DemoQuestion` |
| `src/TrainArena/Data/QuizImport.cs` | Variable options + E/F; `ImportedQuestion.Options` |
| `src/TrainArena/Game/GameSession.cs` | `StartQuestion` length; answer bounds; 50/50 mask count |
| `src/TrainArena/Game/PowerUps/PlayerPowerUpState.cs` | Comment: mask length 1 or 2 |
| `src/TrainArena/Pages/Editor/Edit.cshtml(.cs)` | Type selector; dynamic options; TrueFalse path |
| `src/TrainArena/wwwroot/player/player.js` | Disable 50/50 when ≤2 options; toast for mask count |
| `README.md` | Import docs A–F / 2–6 |
| `tests/TrainArena.Tests/QuizRulesTests.cs` | Rewrite for new API |
| `tests/TrainArena.Tests/QuizImportTests.cs` | 2-opt, E/F, gap skip |
| `tests/TrainArena.Tests/PowerUpUseTests.cs` | 50/50 scale + reject |
| `tests/TrainArena.Tests/GameSessionQuestionTests.cs` | Variable option start/answer (extend or add) |
| `tests/TrainArena.Tests/QuizPersistenceTests.cs` | Persist Option4/5 + DisplayKind if needed |

---

### Task 1: Entity, schema, QuizRules

**Files:**
- Create: `src/TrainArena/Data/Entities/QuestionDisplayKind.cs`
- Modify: `src/TrainArena/Data/Entities/Question.cs`
- Modify: `src/TrainArena/Data/AppDbContext.cs`
- Modify: `src/TrainArena/Data/QuizRules.cs`
- Test: `tests/TrainArena.Tests/QuizRulesTests.cs`

**Interfaces:**
- Produces:
  - `enum QuestionDisplayKind { Mc = 0, TrueFalse = 1 }`
  - `Question.Option4`, `Option5` (`string`, default `""`), `DisplayKind`
  - `QuizRules.ValidateQuestion(string text, IReadOnlyList<string> options, int correctIndex, QuestionDisplayKind kind = QuestionDisplayKind.Mc) → string?`
  - `QuizRules.GetFilledOptions(Question q) → IReadOnlyList<string>`
  - `QuizRules.ToDemoQuestion(Question q)` — only filled options
- Consumes: existing `Question`, `DemoQuestion`

- [ ] **Step 1: Rewrite failing QuizRules tests**

Replace `tests/TrainArena.Tests/QuizRulesTests.cs` with:

```csharp
using TrainArena.Data;
using TrainArena.Data.Entities;

namespace TrainArena.Tests;

public class QuizRulesTests
{
    [Fact]
    public void ValidateQuestion_RejectsEmptyText()
    {
        var error = QuizRules.ValidateQuestion("", ["a", "b", "c", "d"], 0);
        Assert.Equal("Fragetext darf nicht leer sein.", error);
    }

    [Fact]
    public void ValidateQuestion_RejectsFewerThanTwoOptions()
    {
        var error = QuizRules.ValidateQuestion("Q?", ["a"], 0);
        Assert.Equal("Mindestens zwei Antwortoptionen sind Pflicht.", error);
    }

    [Fact]
    public void ValidateQuestion_RejectsMoreThanSixOptions()
    {
        var error = QuizRules.ValidateQuestion("Q?", ["a", "b", "c", "d", "e", "f", "g"], 0);
        Assert.Equal("Maximal sechs Antwortoptionen erlaubt.", error);
    }

    [Fact]
    public void ValidateQuestion_RejectsBlankGap()
    {
        var error = QuizRules.ValidateQuestion("Q?", ["a", "b", "", "d"], 0);
        Assert.Equal("Antwortoptionen dürfen keine Lücken haben.", error);
    }

    [Fact]
    public void ValidateQuestion_RejectsInvalidCorrectIndex()
    {
        var error = QuizRules.ValidateQuestion("Q?", ["a", "b"], 2);
        Assert.Equal("Genau eine richtige Antwort innerhalb der Optionen wählen.", error);
    }

    [Fact]
    public void ValidateQuestion_AcceptsTwoToSixOptions()
    {
        Assert.Null(QuizRules.ValidateQuestion("Q?", ["a", "b"], 1));
        Assert.Null(QuizRules.ValidateQuestion("Q?", ["a", "b", "c"], 2));
        Assert.Null(QuizRules.ValidateQuestion("Q?", ["a", "b", "c", "d", "e", "f"], 5));
    }

    [Fact]
    public void ValidateQuestion_TrueFalse_RequiresExactlyTwo()
    {
        var error = QuizRules.ValidateQuestion("Q?", ["Wahr", "Falsch", "Vielleicht"], 0, QuestionDisplayKind.TrueFalse);
        Assert.Equal("Wahr/Falsch erlaubt genau zwei Optionen.", error);
        Assert.Null(QuizRules.ValidateQuestion("Q?", ["Wahr", "Falsch"], 0, QuestionDisplayKind.TrueFalse));
    }

    [Fact]
    public void GetFilledOptions_StopsAtFirstEmpty()
    {
        var q = new Question
        {
            Text = "Q?",
            Option0 = "a",
            Option1 = "b",
            Option2 = "c",
            Option3 = "",
            Option4 = "",
            Option5 = ""
        };
        Assert.Equal(["a", "b", "c"], QuizRules.GetFilledOptions(q));
    }

    [Fact]
    public void ToDemoQuestion_MapsOnlyFilledOptions()
    {
        var demo = QuizRules.ToDemoQuestion(new Question
        {
            Text = "TF?",
            Option0 = "Wahr",
            Option1 = "Falsch",
            CorrectIndex = 1,
            TimeLimitSeconds = 15,
            DisplayKind = QuestionDisplayKind.TrueFalse
        });
        Assert.Equal(["Wahr", "Falsch"], demo.Options);
        Assert.Equal(1, demo.CorrectIndex);
        Assert.Equal(15, demo.TimeLimitSeconds);
    }

    [Fact]
    public void ToDemoQuestion_MapsImagePathToImageUrl()
    {
        var demo = QuizRules.ToDemoQuestion(new Question
        {
            Text = "Mit Bild?",
            Option0 = "a",
            Option1 = "b",
            Option2 = "c",
            Option3 = "d",
            CorrectIndex = 0,
            TimeLimitSeconds = 20,
            ImagePath = "/uploads/abc.png"
        });
        Assert.Equal("/uploads/abc.png", demo.ImageUrl);
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~QuizRulesTests --nologo
```

Expected: FAIL (missing members / old `ValidateQuestion` signature).

- [ ] **Step 3: Implement enum + entity fields**

`QuestionDisplayKind.cs`:

```csharp
namespace TrainArena.Data.Entities;

public enum QuestionDisplayKind
{
    Mc = 0,
    TrueFalse = 1
}
```

Add to `Question.cs`:

```csharp
public string Option4 { get; set; } = "";
public string Option5 { get; set; } = "";
public QuestionDisplayKind DisplayKind { get; set; } = QuestionDisplayKind.Mc;
```

- [ ] **Step 4: Implement `QuizRules`**

```csharp
using TrainArena.Data.Entities;
using TrainArena.Game;

namespace TrainArena.Data;

public static class QuizRules
{
    public const int MinOptions = 2;
    public const int MaxOptions = 6;

    public static string? ValidateQuestion(
        string text,
        IReadOnlyList<string> options,
        int correctIndex,
        QuestionDisplayKind kind = QuestionDisplayKind.Mc)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Fragetext darf nicht leer sein.";
        }

        ArgumentNullException.ThrowIfNull(options);

        for (var i = 0; i < options.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(options[i]))
            {
                return "Antwortoptionen dürfen keine Lücken haben.";
            }
        }

        if (options.Count < MinOptions)
        {
            return "Mindestens zwei Antwortoptionen sind Pflicht.";
        }

        if (options.Count > MaxOptions)
        {
            return "Maximal sechs Antwortoptionen erlaubt.";
        }

        if (kind == QuestionDisplayKind.TrueFalse && options.Count != 2)
        {
            return "Wahr/Falsch erlaubt genau zwei Optionen.";
        }

        if (correctIndex < 0 || correctIndex >= options.Count)
        {
            return "Genau eine richtige Antwort innerhalb der Optionen wählen.";
        }

        return null;
    }

    public static IReadOnlyList<string> GetFilledOptions(Question q)
    {
        ArgumentNullException.ThrowIfNull(q);
        string[] all = [q.Option0, q.Option1, q.Option2, q.Option3, q.Option4, q.Option5];
        var list = new List<string>(MaxOptions);
        foreach (var opt in all)
        {
            if (string.IsNullOrWhiteSpace(opt))
            {
                break;
            }

            list.Add(opt.Trim());
        }

        return list;
    }

    public static void ApplyOptions(Question q, IReadOnlyList<string> options)
    {
        ArgumentNullException.ThrowIfNull(q);
        ArgumentNullException.ThrowIfNull(options);
        string[] slots = ["", "", "", "", "", ""];
        for (var i = 0; i < options.Count && i < MaxOptions; i++)
        {
            slots[i] = options[i].Trim();
        }

        q.Option0 = slots[0];
        q.Option1 = slots[1];
        q.Option2 = slots[2];
        q.Option3 = slots[3];
        q.Option4 = slots[4];
        q.Option5 = slots[5];
    }

    public static DemoQuestion ToDemoQuestion(Question q) => new()
    {
        Text = q.Text,
        Options = GetFilledOptions(q).ToArray(),
        CorrectIndex = q.CorrectIndex,
        TimeLimitSeconds = q.TimeLimitSeconds <= 0 ? 20 : q.TimeLimitSeconds,
        ImageUrl = string.IsNullOrWhiteSpace(q.ImagePath) ? null : q.ImagePath
    };
}
```

- [ ] **Step 5: EF + schema patch**

In `AppDbContext.OnModelCreating` for `Question`:

- Keep `Option0`/`Option1` required max 400
- `Option2`…`Option5`: max 400, required (empty string OK — not null)
- `DisplayKind`: store as int

Extend `EnsureSchemaAsync` (same pattern as `ImagePath`) to add missing columns:

```sql
ALTER TABLE Questions ADD COLUMN Option4 TEXT NOT NULL DEFAULT '';
ALTER TABLE Questions ADD COLUMN Option5 TEXT NOT NULL DEFAULT '';
ALTER TABLE Questions ADD COLUMN DisplayKind INTEGER NOT NULL DEFAULT 0;
```

Only run each `ALTER` if the column is missing (`PRAGMA table_info`).

- [ ] **Step 6: Fix compile breakages from old `ValidateQuestion(text, o0, o1, o2, o3, idx)`**

Update call sites temporarily to pass arrays (Editor + Import) so the solution builds — full Import rewrite is Task 3; for now:

```csharp
QuizRules.ValidateQuestion(text, [option0, option1, option2, option3], correctIndex);
```

(Import still requires 4 filled options until Task 3.)

- [ ] **Step 7: Run QuizRules tests — expect PASS**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~QuizRulesTests --nologo
```

- [ ] **Step 8: Commit**

```bash
git add src/TrainArena/Data tests/TrainArena.Tests/QuizRulesTests.cs src/TrainArena/Pages/Editor
git commit -m "feat(quiz): Option4/5 + DisplayKind and flexible QuizRules"
```

---

### Task 2: GameSession — variable options + 50/50 scaling

**Files:**
- Modify: `src/TrainArena/Game/GameSession.cs` (`StartQuestion`, `SubmitAnswer`, `PickTwoWrongIndexes`)
- Modify: `src/TrainArena/Game/PowerUps/PlayerPowerUpState.cs` (comment)
- Test: `tests/TrainArena.Tests/PowerUpUseTests.cs`
- Test: `tests/TrainArena.Tests/GameSessionQuestionTests.cs` (add cases)

**Interfaces:**
- Consumes: `DemoQuestion.Options` length 2–6
- Produces: `PickWrongIndexes(correctIndex, optionCount, maskCount) → int[]`; 50/50 uses `maskCount = optionCount >= 4 ? 2 : optionCount == 3 ? 1 : 0`

- [ ] **Step 1: Failing tests**

Add to `PowerUpUseTests.cs`:

```csharp
[Fact]
public void FiftyFifty_OnThreeOptions_MasksOneWrong()
{
    var s = new GameSession("ABC123", "host", Guid.NewGuid(), PowerUpRoomConfig.CreateDefault());
    s.TryJoin("Ada", "p1");
    var q = new DemoQuestion { Text = "Q?", Options = ["a", "b", "c"], CorrectIndex = 0, TimeLimitSeconds = 20 };
    s.SetQuestions([q]);
    Assert.True(s.StartQuestion(q, DateTimeOffset.UtcNow).ok);
    var (ok, err, result) = s.TryUsePowerUp("p1", PowerUpId.FiftyFifty);
    Assert.True(ok, err);
    Assert.NotNull(result!.MaskedWrongIndexes);
    Assert.Single(result.MaskedWrongIndexes!);
    Assert.DoesNotContain(0, result.MaskedWrongIndexes);
}

[Fact]
public void FiftyFifty_OnTwoOptions_Rejected()
{
    var s = new GameSession("ABC123", "host", Guid.NewGuid(), PowerUpRoomConfig.CreateDefault());
    s.TryJoin("Ada", "p1");
    var q = new DemoQuestion { Text = "Q?", Options = ["Wahr", "Falsch"], CorrectIndex = 0, TimeLimitSeconds = 20 };
    s.SetQuestions([q]);
    Assert.True(s.StartQuestion(q, DateTimeOffset.UtcNow).ok);
    var (ok, err, _) = s.TryUsePowerUp("p1", PowerUpId.FiftyFifty);
    Assert.False(ok);
    Assert.Contains("50/50", err!, StringComparison.OrdinalIgnoreCase);
}
```

Add to `GameSessionQuestionTests.cs` (or create if missing suitable file):

```csharp
[Fact]
public void StartQuestion_AcceptsTwoToSixOptions()
{
    var s = new GameSession("ABC123", "host", Guid.NewGuid());
    s.TryJoin("Ada", "p1");
    var q = new DemoQuestion { Text = "Q?", Options = ["a", "b"], CorrectIndex = 1, TimeLimitSeconds = 20 };
    s.SetQuestions([q]);
    Assert.True(s.StartQuestion(q, DateTimeOffset.UtcNow).ok);
}

[Fact]
public void SubmitAnswer_RejectsIndexPastOptionCount()
{
    var s = new GameSession("ABC123", "host", Guid.NewGuid());
    s.TryJoin("Ada", "p1");
    var q = new DemoQuestion { Text = "Q?", Options = ["a", "b"], CorrectIndex = 0, TimeLimitSeconds = 20 };
    s.SetQuestions([q]);
    Assert.True(s.StartQuestion(q, DateTimeOffset.UtcNow).ok);
    var (ok, err, _) = s.SubmitAnswer("p1", 2, DateTimeOffset.UtcNow);
    Assert.False(ok);
    Assert.Contains("Invalid option", err!, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run — expect FAIL**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter "FullyQualifiedName~PowerUpUseTests|FullyQualifiedName~GameSessionQuestionTests" --nologo
```

Expected: FAIL on StartQuestion length check and/or 50/50 hard-coded to 4.

- [ ] **Step 3: Implement session changes**

In `StartQuestion`, replace length/correct checks:

```csharp
var n = question.Options.Length;
if (n is < 2 or > 6)
{
    return (false, "Question must have 2–6 options");
}

if (question.CorrectIndex < 0 || question.CorrectIndex >= n)
{
    return (false, "Invalid correct index");
}
```

In `SubmitAnswer`, replace `optionIndex is < 0 or > 3` with:

```csharp
if (optionIndex < 0 || optionIndex >= CurrentQuestion.Options.Length)
{
    return (false, "Invalid option", 0);
}
```

In FiftyFifty case, before masking:

```csharp
var optionCount = CurrentQuestion.Options.Length;
var maskCount = optionCount >= 4 ? 2 : optionCount == 3 ? 1 : 0;
if (maskCount == 0)
{
    return (false, "50/50 requires at least 3 options", null);
}
masked = PickWrongIndexes(CurrentQuestion.CorrectIndex, optionCount, maskCount);
```

Replace `PickTwoWrongIndexes` with:

```csharp
private static int[] PickWrongIndexes(int correctIndex, int optionCount, int maskCount)
{
    var wrong = Enumerable.Range(0, optionCount).Where(i => i != correctIndex).ToList();
    for (var i = wrong.Count - 1; i > 0; i--)
    {
        var j = Random.Shared.Next(i + 1);
        (wrong[i], wrong[j]) = (wrong[j], wrong[i]);
    }

    return wrong.Take(maskCount).ToArray();
}
```

Update `PlayerPowerUpState` comment: `MaskedWrongIndexes` length 1 or 2.

- [ ] **Step 4: Run tests — expect PASS**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter "FullyQualifiedName~PowerUpUseTests|FullyQualifiedName~GameSessionQuestionTests" --nologo
```

Also run full suite once to catch hard-coded “4 options” elsewhere:

```bash
dotnet test TrainArena.sln --nologo
```

- [ ] **Step 5: Commit**

```bash
git add src/TrainArena/Game tests/TrainArena.Tests/PowerUpUseTests.cs tests/TrainArena.Tests/GameSessionQuestionTests.cs
git commit -m "feat(game): allow 2–6 options and scale 50/50 masks"
```

---

### Task 3: CSV / Kahoot import (2–6 + E/F)

**Files:**
- Modify: `src/TrainArena/Data/QuizImport.cs`
- Test: `tests/TrainArena.Tests/QuizImportTests.cs`

**Interfaces:**
- Produces: `ImportedQuestion(string Text, IReadOnlyList<string> Options, int CorrectIndex, int TimeLimitSeconds)`
- `ToEntity` uses `QuizRules.ApplyOptions` + `DisplayKind = Mc`
- Column map: optional E/F (Answer 5/6); A–D headers still required for format detection; empty C/D cells allowed when only 2 answers filled
- `TryParseCorrect`: letters A–F; TrainArena 0–5 (and 1–6 convenience); Kahoot 1–6 one-based

- [ ] **Step 1: Update / add import tests**

Keep `ParseCsv_MapsFourOptionRows` and Kahoot 4-answer test working (map `Option2` via `Options[2]`).

Change `ParseCsv_SkipsInvalidRowsAndReportsErrors` — “Nur drei” with `a,b,c,,0` is now **valid** (3 options). Use a gap row instead:

```csharp
[Fact]
public void ParseCsv_SkipsInvalidRowsAndReportsErrors()
{
    var csv = """
        Question,OptionA,OptionB,OptionC,OptionD,Correct
        Gut,a,b,c,d,1
        ,a,b,c,d,0
        Lücke,a,,c,d,0
        """;

    var result = QuizImport.Parse(csv);

    Assert.Single(result.Questions);
    Assert.Equal("Gut", result.Questions[0].Text);
    Assert.Equal(1, result.Questions[0].CorrectIndex);
    Assert.True(result.Errors.Count >= 2);
}

[Fact]
public void ParseCsv_AcceptsTwoOptionsAndOptionalEF()
{
    var csv = """
        Question,OptionA,OptionB,OptionC,OptionD,OptionE,OptionF,Correct,TimeLimitSeconds
        Wahr?,Ja,Nein,,,,A,15
        Sechs?,a,b,c,d,e,f,F,20
        """;

    var result = QuizImport.Parse(csv);

    Assert.Empty(result.Errors);
    Assert.Equal(2, result.Questions.Count);
    Assert.Equal(["Ja", "Nein"], result.Questions[0].Options);
    Assert.Equal(0, result.Questions[0].CorrectIndex);
    Assert.Equal(15, result.Questions[0].TimeLimitSeconds);
    Assert.Equal(6, result.Questions[1].Options.Count);
    Assert.Equal(5, result.Questions[1].CorrectIndex);
}

[Fact]
public void ParseKahootLikeCsv_AcceptsTwoAnswers()
{
    var csv = """
        Question,Answer 1,Answer 2,Answer 3,Answer 4,Time limit,Correct answer(s)
        Himmel blau?,Wahr,Falsch,,,20,1
        """;

    var result = QuizImport.Parse(csv);

    Assert.Empty(result.Errors);
    Assert.Single(result.Questions);
    Assert.Equal(["Wahr", "Falsch"], result.Questions[0].Options);
    Assert.Equal(0, result.Questions[0].CorrectIndex);
}
```

Update four-option assertions to use `.Options[n]` instead of `.Option2` if the record shape changes.

- [ ] **Step 2: Run — expect FAIL**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~QuizImportTests --nologo
```

- [ ] **Step 3: Implement import**

Key algorithm after reading cells:

```csharp
var rawOptions = new[] { o0, o1, o2, o3, o4, o5 }; // o4/o5 from optional columns or ""
var filled = new List<string>();
var sawEmpty = false;
foreach (var raw in rawOptions)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        sawEmpty = true;
        continue;
    }

    if (sawEmpty)
    {
        // gap after a filled option later — reject via validation message
        filled = null!; // or set gap flag
        break;
    }

    filled.Add(raw.Trim());
}
```

Simpler: build list stopping at first empty; if any non-empty appears after that empty among o0…o5, treat as gap error.

Validate with `QuizRules.ValidateQuestion(text, filled, correctIndex)`.

`TryParseCorrect`: extend letter range to `F`; one-based 1–6; zero-based 0–5 (+ convenience 1–6 when not one-based — keep existing 1–4 convenience extended to 1–6).

`ColumnMap` gains `int? Option4`, `int? Option5`. TrainArena header detection: still require A–D + Correct; look up `optione`/`option4`/`e` and `optionf`/`option5`/`f`. Kahoot: require Answer1–4; optional Answer5–6.

`ToEntity`:

```csharp
public static Question ToEntity(ImportedQuestion q, Guid quizId, int sortOrder)
{
    var entity = new Question
    {
        Id = Guid.NewGuid(),
        QuizId = quizId,
        Text = q.Text,
        CorrectIndex = q.CorrectIndex,
        TimeLimitSeconds = q.TimeLimitSeconds,
        SortOrder = sortOrder,
        DisplayKind = QuestionDisplayKind.Mc
    };
    QuizRules.ApplyOptions(entity, q.Options);
    return entity;
}
```

- [ ] **Step 4: Run import tests — PASS**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~QuizImportTests --nologo
```

- [ ] **Step 5: Commit**

```bash
git add src/TrainArena/Data/QuizImport.cs tests/TrainArena.Tests/QuizImportTests.cs
git commit -m "feat(import): allow 2–6 options with optional E/F columns"
```

---

### Task 4: Editor UX (MC 2–6 + Wahr/Falsch)

**Files:**
- Modify: `src/TrainArena/Pages/Editor/Edit.cshtml`
- Modify: `src/TrainArena/Pages/Editor/Edit.cshtml.cs`
- Test: manual + `QuizPersistenceTests` if useful; prefer a small persistence test for TrueFalse round-trip

**Interfaces:**
- Consumes: `QuizRules.ValidateQuestion`, `ApplyOptions`, `GetFilledOptions`, `QuestionDisplayKind`
- Form posts: `displayKind`, `option0`…`option5` (unused slots empty), `correctIndex`

- [ ] **Step 1: Persistence test (optional but recommended)**

```csharp
[Fact]
public async Task AddQuestion_PersistsTwoOptionsAndTrueFalseKind()
{
    // Use existing WebApplicationFactory / DbContext pattern from QuizPersistenceTests
    // Create quiz, add Question with DisplayKind=TrueFalse, Option0=Wahr, Option1=Falsch
    // Reload and assert GetFilledOptions length 2 and DisplayKind
}
```

Follow the factory pattern already in `QuizPersistenceTests.cs`.

- [ ] **Step 2: Update `OnPostAddQuestionAsync` signature**

```csharp
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
```

Parse kind:

```csharp
var kind = string.Equals(displayKind, "TrueFalse", StringComparison.OrdinalIgnoreCase)
    ? QuestionDisplayKind.TrueFalse
    : QuestionDisplayKind.Mc;

if (kind == QuestionDisplayKind.TrueFalse)
{
    option0 = "Wahr";
    option1 = "Falsch";
    option2 = option3 = option4 = option5 = "";
}
```

Build options list via stop-at-empty; validate; `ApplyOptions`; set `DisplayKind`.

- [ ] **Step 3: Edit.cshtml UI**

- Select „Fragetyp“: Multiple Choice | Wahr/Falsch  
- MC: six inputs (A–F); C–F not `required`; small JS to show/hide or enable add/remove — minimal approach: always show A–F, leave unused blank; update correct-answer `<select>` options dynamically based on non-empty fields (inline script OK)  
- TrueFalse: hide MC inputs; show radio Wahr/Falsch for `correctIndex`  
- List view: iterate `QuizRules.GetFilledOptions(q)`; badge if `TrueFalse`

Default new MC form: prefill empty A–D visible mindset — leave E/F empty (4-option default behavior preserved when user fills A–D).

- [ ] **Step 4: Run persistence test + full solution build**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~QuizPersistenceTests --nologo
dotnet build TrainArena.sln --nologo
```

- [ ] **Step 5: Commit**

```bash
git add src/TrainArena/Pages/Editor tests/TrainArena.Tests/QuizPersistenceTests.cs
git commit -m "feat(editor): MC 2–6 options and Wahr/Falsch type"
```

---

### Task 5: Player UI — disable 50/50 when ≤2 options

**Files:**
- Modify: `src/TrainArena/wwwroot/player/player.js`

**Interfaces:**
- Consumes: `QuestionStartedMessage.options.length`
- Produces: 50/50 button disabled when `options.length <= 2`; toast text reflects mask size when applied

- [ ] **Step 1: Track current option count**

```javascript
let currentOptionCount = 0;
```

On `QuestionStarted`:

```javascript
currentOptionCount = (msg.options || []).length;
```

On question end / leave: reset to `0`.

- [ ] **Step 2: Gate in `refreshPowerUpButtons` / `usePowerUp`**

```javascript
function fiftyFiftyAllowed() {
  return currentOptionCount >= 3;
}
```

When refreshing the fifty_fifty button: if `!fiftyFiftyAllowed()`, force `disabled = true` (even if inventory &gt; 0). In `usePowerUp`, early-return if `id === "fifty_fifty" && !fiftyFiftyAllowed()`.

- [ ] **Step 3: Toast copy**

When applying mask:

```javascript
const n = (msg.maskedWrongIndexes || []).length;
showPuToast(n === 1 ? "50/50 — eine Option entfernt" : "50/50 — zwei Optionen entfernt");
```

- [ ] **Step 4: Manual smoke (after `dotnet run`)**

1. Host: quiz with Wahr/Falsch → Player: 50/50 disabled  
2. Host: 3-option MC → 50/50 masks one  
3. Host: 4-option → masks two (regression)

- [ ] **Step 5: Commit**

```bash
git add src/TrainArena/wwwroot/player/player.js
git commit -m "fix(player): disable 50/50 on two-option questions"
```

---

### Task 6: README + full regression

**Files:**
- Modify: `README.md` (Import section + Features bullet)
- Test: full `dotnet test TrainArena.sln`

- [ ] **Step 1: README Import section**

Document:

- Options A–F; at least two filled; optional E/F  
- `Correct`: `A`–`F` or `0`–`5` (also `1`–`6`)  
- Kahoot Answer 1–6  
- Editor: Wahr/Falsch + MC 2–6  
- 50/50 scales with option count  

Update Features list: „Text-MC-Editor (2–6 Optionen, 1 richtig) + Wahr/Falsch + optionale Bilder“.

- [ ] **Step 2: Full test suite**

```bash
dotnet test TrainArena.sln --nologo
```

Expected: all green. Fix any remaining hard-coded four-option assumptions in tests (search for `Options.Length != 4`, `Alle vier`, `Option3` required in validation messages).

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: document 2–6 options and Wahr/Falsch"
```

---

## Spec coverage checklist

| Spec item | Task |
|-----------|------|
| Option0–5 + DisplayKind | 1 |
| QuizRules 2–6, no gaps, TrueFalse | 1 |
| EnsureSchema patches | 1 |
| StartQuestion / SubmitAnswer bounds | 2 |
| 50/50 scale 2/1/reject | 2 |
| Import E/F + 2-opt + legacy 4 | 3 |
| Editor MC + TrueFalse UI | 4 |
| Player disable 50/50 | 5 |
| README | 6 |
| Frage-Review | Explicitly out of scope |

## Placeholder / consistency self-review

- No TBD steps; APIs named consistently (`GetFilledOptions`, `ApplyOptions`, `PickWrongIndexes`, `QuestionDisplayKind`)  
- `ImportedQuestion.Options` replaces Option0–3 fields everywhere in Task 3  
- Toast + server error strings are specified  
