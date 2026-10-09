# Frage-Review Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** At Finale, Host sees per-question % correct and answer distribution under the ranking (live session memory only).

**Architecture:** Snapshot `QuestionReviewStat` on each Reveal (while `_answersThisQuestion` still holds picks). Append to `GameSession.Review`. Extend `GameFinishedMessage` with `Review` DTOs; Host JS renders under finale. Players ignore the field. No SQLite.

**Tech Stack:** .NET 10 · SignalR · vanilla Host JS · xUnit

## Global Constraints

- Spec: `docs/superpowers/specs/2026-10-09-frage-review-design.md`
- Host only; player UI unchanged (may receive but must not render Review)
- Content: % correct + distribution; no nicknames per option
- Only at Finale under ranking; quiz order
- CorrectCount = chosen index == CorrectIndex (not points / not disruption-gated)
- Live memory only; no schema change
- German Host copy; TDD; branch `cursor/frage-review-design-b12d`

---

## File map

| File | Role |
|------|------|
| `src/TrainArena/Game/QuestionReviewStat.cs` | Immutable snapshot type |
| `src/TrainArena/Game/GameSession.cs` | `Review` list; capture in `TransitionQuestionToReveal` |
| `src/TrainArena/Contracts/HubMessages.cs` | `QuestionReviewDto`; extend `GameFinishedMessage` |
| `src/TrainArena/Hubs/GameHub.cs` | Build Review into Finished (+ sync) |
| `src/TrainArena/Pages/Index.cshtml` | Host markup container `#review` |
| `src/TrainArena/wwwroot/js/host.js` | Render review on `GameFinished` |
| `src/TrainArena/wwwroot/css/host.css` | Minimal review layout |
| `README.md` | One sentence under Features / Host flow |
| `tests/TrainArena.Tests/QuestionReviewTests.cs` | Session snapshot unit tests |
| `tests/TrainArena.Tests/FullRoundTests.cs` | Assert `GameFinished.Review` |
| Other tests constructing `GameFinishedMessage` | Add `Review` arg |

---

### Task 1: Session snapshot (`QuestionReviewStat`)

**Files:**
- Create: `src/TrainArena/Game/QuestionReviewStat.cs`
- Modify: `src/TrainArena/Game/GameSession.cs`
- Test: `tests/TrainArena.Tests/QuestionReviewTests.cs`

**Interfaces:**
- Produces:
  - `QuestionReviewStat` with `QuestionIndex`, `Text`, `Options`, `CorrectIndex`, `Counts`, `AnsweredCount`, `PlayerCount`, `CorrectCount`
  - `GameSession.Review` → `IReadOnlyList<QuestionReviewStat>`
  - Capture inside `TransitionQuestionToReveal` before any clear of answers (answers are cleared on next `StartQuestion`, not on Reveal — still capture at Reveal for a single source of truth)

- [ ] **Step 1: Failing tests**

```csharp
using TrainArena.Game;
using TrainArena.Game.PowerUps;

namespace TrainArena.Tests;

public class QuestionReviewTests
{
    private static (GameSession s, DemoQuestion q0, DemoQuestion q1) TwoQuestionRoom()
    {
        var s = new GameSession("ABC123", "host", Guid.NewGuid());
        s.TryJoin("Ada", "p1");
        s.TryJoin("Bob", "p2");
        var q0 = new DemoQuestion { Text = "Q0?", Options = ["a", "b", "c"], CorrectIndex = 0, TimeLimitSeconds = 20 };
        var q1 = new DemoQuestion { Text = "Q1?", Options = ["Wahr", "Falsch"], CorrectIndex = 1, TimeLimitSeconds = 20 };
        s.SetQuestions([q0, q1]);
        return (s, q0, q1);
    }

    [Fact]
    public void Reveal_AppendsReviewStat_WithCountsAndCorrect()
    {
        var (s, q0, _) = TwoQuestionRoom();
        Assert.True(s.StartQuestion(q0, DateTimeOffset.UtcNow).ok);
        s.SubmitAnswer("p1", 0, DateTimeOffset.UtcNow); // correct
        s.SubmitAnswer("p2", 2, DateTimeOffset.UtcNow); // wrong
        Assert.True(s.ForceEndQuestion());

        Assert.Single(s.Review);
        var r = s.Review[0];
        Assert.Equal(0, r.QuestionIndex);
        Assert.Equal("Q0?", r.Text);
        Assert.Equal(["a", "b", "c"], r.Options);
        Assert.Equal(0, r.CorrectIndex);
        Assert.Equal([1, 0, 1], r.Counts);
        Assert.Equal(2, r.AnsweredCount);
        Assert.Equal(2, r.PlayerCount);
        Assert.Equal(1, r.CorrectCount);
    }

    [Fact]
    public void Reveal_ZeroAnswers_StillListed()
    {
        var (s, q0, _) = TwoQuestionRoom();
        Assert.True(s.StartQuestion(q0, DateTimeOffset.UtcNow).ok);
        Assert.True(s.ForceEndQuestion());

        Assert.Single(s.Review);
        Assert.Equal([0, 0, 0], s.Review[0].Counts);
        Assert.Equal(0, s.Review[0].AnsweredCount);
        Assert.Equal(2, s.Review[0].PlayerCount);
        Assert.Equal(0, s.Review[0].CorrectCount);
    }

    [Fact]
    public void Reveal_DisruptedCorrectPick_CountsAsCorrect()
    {
        var cfg = PowerUpRoomConfig.CreateDefault();
        cfg.Starter[PowerUpId.Disrupt] = 1;
        cfg.Starter[PowerUpId.Shield] = 0;
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), cfg);
        s.TryJoin("Ada", "p1");
        s.TryJoin("Bob", "p2");
        var q = new DemoQuestion { Text = "Q?", Options = ["a", "b"], CorrectIndex = 0, TimeLimitSeconds = 20 };
        s.SetQuestions([q]);
        Assert.True(s.StartQuestion(q, DateTimeOffset.UtcNow).ok);
        Assert.True(s.TryUsePowerUp("p1", PowerUpId.Disrupt, "Bob").ok);
        s.SubmitAnswer("p2", 0, DateTimeOffset.UtcNow); // correct option, disrupted
        s.SubmitAnswer("p1", 1, DateTimeOffset.UtcNow);
        Assert.True(s.ForceEndQuestion());

        Assert.Equal(1, s.Review[0].CorrectCount);
        Assert.Equal([1, 1], s.Review[0].Counts);
    }

    [Fact]
    public void TwoReveals_AppendTwoStats()
    {
        var (s, q0, q1) = TwoQuestionRoom();
        Assert.True(s.StartQuestion(q0, DateTimeOffset.UtcNow).ok);
        s.SubmitAnswer("p1", 0, DateTimeOffset.UtcNow);
        Assert.True(s.ForceEndQuestion());
        s.ShowLeaderboard();
        Assert.True(s.StartQuestion(q1, DateTimeOffset.UtcNow.AddMinutes(1)).ok);
        s.SubmitAnswer("p1", 1, DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.True(s.ForceEndQuestion());

        Assert.Equal(2, s.Review.Count);
        Assert.Equal(1, s.Review[1].QuestionIndex);
        Assert.Equal(2, s.Review[1].Options.Length);
    }
}
```

Adjust Disrupt setup if starter keys / TryUsePowerUp signature differ — match existing `PowerUpUseTests` / disrupt tests in the repo.

- [ ] **Step 2: Run — expect FAIL**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~QuestionReviewTests --nologo
```

- [ ] **Step 3: Implement types + capture**

`QuestionReviewStat.cs`:

```csharp
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
```

On `GameSession`:

```csharp
private readonly List<QuestionReviewStat> _review = [];
public IReadOnlyList<QuestionReviewStat> Review
{
    get { lock (_gate) return _review.ToList(); }
}
```

In `TransitionQuestionToReveal` (must hold `_gate` — callers already lock):

```csharp
private void TransitionQuestionToReveal()
{
    CaptureReviewStatUnlocked();
    ClearDoubleActiveForAllPlayers();
    Phase = GamePhase.Reveal;
}

private void CaptureReviewStatUnlocked()
{
    if (CurrentQuestion is null) return;
    var options = CurrentQuestion.Options;
    var counts = new int[options.Length];
    var correct = 0;
    foreach (var (_, optionIndex) in _answersThisQuestion)
    {
        if (optionIndex >= 0 && optionIndex < counts.Length)
        {
            counts[optionIndex]++;
            if (optionIndex == CurrentQuestion.CorrectIndex) correct++;
        }
    }

    var playerCount = _players.Count(p => p.IsConnected);
    _review.Add(new QuestionReviewStat
    {
        QuestionIndex = QuestionIndex, // confirm this matches 0-based current; if QuestionIndex is incremented at Start, use the index already used for hub messages
        Text = CurrentQuestion.Text,
        Options = options.ToArray(),
        CorrectIndex = CurrentQuestion.CorrectIndex,
        Counts = counts,
        AnsweredCount = _answersThisQuestion.Count,
        PlayerCount = playerCount,
        CorrectCount = correct
    });
}
```

**Index caveat:** Inspect `StartQuestion` — if `QuestionIndex++` happens at start, the review index for the first question is already `0` after increment from `-1` or similar. Match whatever `QuestionEndedMessage.Index` uses today (same source).

- [ ] **Step 4: Run QuestionReviewTests — PASS**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~QuestionReviewTests --nologo
```

- [ ] **Step 5: Commit**

```bash
git add src/TrainArena/Game/QuestionReviewStat.cs src/TrainArena/Game/GameSession.cs tests/TrainArena.Tests/QuestionReviewTests.cs
git commit -m "feat(game): snapshot per-question review stats on Reveal"
```

---

### Task 2: Hub wire + Finished sync

**Files:**
- Modify: `src/TrainArena/Contracts/HubMessages.cs`
- Modify: `src/TrainArena/Hubs/GameHub.cs`
- Modify: `tests/TrainArena.Tests/FullRoundTests.cs` (+ any compile breaks on `GameFinishedMessage`)
- Test: extend FullRound / add focused hub assertion

**Interfaces:**
- Produces:
  - `QuestionReviewDto(int Index, string Text, string[] Options, int CorrectIndex, int[] Counts, int AnsweredCount, int PlayerCount, int CorrectCount)`
  - `GameFinishedMessage(IReadOnlyList<LeaderboardEntryDto> Entries, IReadOnlyList<QuestionReviewDto> Review)`
- Consumes: `GameSession.Review`

- [ ] **Step 1: Update contract + failing FullRound assertion**

```csharp
public sealed record QuestionReviewDto(
    int Index,
    string Text,
    string[] Options,
    int CorrectIndex,
    int[] Counts,
    int AnsweredCount,
    int PlayerCount,
    int CorrectCount);

public sealed record GameFinishedMessage(
    IReadOnlyList<LeaderboardEntryDto> Entries,
    IReadOnlyList<QuestionReviewDto> Review);
```

In `FullRoundTests.TwoQuestionQuiz_HostNext_AdvancesThenFinishes`, after `var done = await finished`:

```csharp
Assert.Equal(2, done.Review.Count);
Assert.Equal(0, done.Review[0].Index);
Assert.Equal(1, done.Review[1].Index);
Assert.Equal(done.Review[0].Options.Length, done.Review[0].Counts.Length);
```

Fix all `new GameFinishedMessage(...)` call sites (Hub + tests) to pass Review (empty list only if session has none — production always maps `session.Review`).

- [ ] **Step 2: Run — expect FAIL / compile errors**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~FullRoundTests --nologo
```

- [ ] **Step 3: Map in GameHub**

Helper:

```csharp
private static IReadOnlyList<QuestionReviewDto> ToReview(GameSession session) =>
    session.Review.Select(r => new QuestionReviewDto(
        r.QuestionIndex,
        r.Text,
        r.Options,
        r.CorrectIndex,
        r.Counts,
        r.AnsweredCount,
        r.PlayerCount,
        r.CorrectCount)).ToList();

private static GameFinishedMessage ToFinished(GameSession session) =>
    new(ToLeaderboard(session).Entries, ToReview(session));
```

Replace every `new GameFinishedMessage(ToLeaderboard(session).Entries)` with `ToFinished(session)` (broadcast + Finished sync branch).

- [ ] **Step 4: FullRound + solution tests PASS**

```bash
dotnet test TrainArena.sln --nologo
```

- [ ] **Step 5: Commit**

```bash
git add src/TrainArena/Contracts/HubMessages.cs src/TrainArena/Hubs/GameHub.cs tests/TrainArena.Tests
git commit -m "feat(hub): include question review on GameFinished"
```

---

### Task 3: Host UI + README

**Files:**
- Modify: `src/TrainArena/Pages/Index.cshtml`
- Modify: `src/TrainArena/wwwroot/js/host.js`
- Modify: `src/TrainArena/wwwroot/css/host.css`
- Modify: `README.md` (one sentence)
- Manual smoke after `dotnet run`

**Interfaces:**
- Consumes: `msg.review` on `GameFinished` (camelCase from SignalR JSON)

- [ ] **Step 1: Markup**

Inside `#live`, after `#finished` (or after board in finale path):

```html
<section id="review" class="review hidden" aria-label="Fragen-Review">
  <h3>Fragen-Review</h3>
  <div id="review-list"></div>
</section>
```

- [ ] **Step 2: Render in host.js**

```javascript
const reviewEl = $("review");
const reviewList = $("review-list");

function renderReview(items) {
  reviewList.innerHTML = "";
  if (!items || !items.length) {
    reviewEl.classList.add("hidden");
    return;
  }
  items.forEach((r) => {
    const pct = r.answeredCount > 0
      ? Math.round((100 * r.correctCount) / r.answeredCount) + " %"
      : "—";
    const block = document.createElement("article");
    block.className = "review-item";
    const opts = (r.options || []).map((opt, i) => {
      const count = (r.counts && r.counts[i]) || 0;
      const correct = i === r.correctIndex ? " is-correct" : "";
      return `<li class="${correct.trim()}"><span class="opt-index">${i + 1}</span> ${escapeHtml(opt)} <span class="muted">× ${count}</span></li>`;
    }).join("");
    block.innerHTML = `
      <h4>${r.index + 1}. ${escapeHtml(r.text)}</h4>
      <p class="muted">${pct} richtig · ${r.answeredCount} von ${r.playerCount} geantwortet</p>
      <ol class="options review-options">${opts}</ol>`;
    reviewList.appendChild(block);
  });
  reviewEl.classList.remove("hidden");
}

// reuse or add simple escapeHtml
function escapeHtml(s) {
  return String(s)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}
```

In `GameFinished` handler: `renderReview(msg.review);`  
When starting a new room / leaving finale: `reviewEl.classList.add("hidden"); reviewList.innerHTML = "";`

Player `player.js`: no change (ignore `review`).

- [ ] **Step 3: CSS**

Reuse `.options` / `.is-correct` where possible; add spacing for `.review` / `.review-item` (no card clutter — simple section + separators).

- [ ] **Step 4: README**

Under Features / Host flow: one sentence that after the finale the Host sees Fragen-Review (% + Verteilung).

- [ ] **Step 5: Manual smoke**

```bash
dotnet run --project src/TrainArena --urls http://127.0.0.1:5175
```

Host + player: full round → Host finale shows review; player does not.

- [ ] **Step 6: Commit**

```bash
git add src/TrainArena/Pages/Index.cshtml src/TrainArena/wwwroot/js/host.js src/TrainArena/wwwroot/css/host.css README.md
git commit -m "feat(host): render Fragen-Review under finale leaderboard"
```

---

## Spec coverage checklist

| Spec item | Task |
|-----------|------|
| Snapshot on Reveal | 1 |
| CorrectCount = option match | 1 |
| PlayerCount / AnsweredCount | 1 |
| `GameFinished` + DTO | 2 |
| Finished reconnect sync | 2 |
| Host UI under ranking | 3 |
| Player ignores | 3 (no player change) |
| README | 3 |
| No SQLite | all |

## Placeholder / consistency self-review

- Types named `QuestionReviewStat` / `QuestionReviewDto` consistently  
- Capture only in `TransitionQuestionToReveal`  
- No TBD steps; Index aligned with existing question index semantics  
