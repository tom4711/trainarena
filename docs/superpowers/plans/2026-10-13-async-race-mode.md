# Async-Rennen Gamemode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an optional Async-Rennen room mode where each player gets a shuffled question order and personal timers, while the host sees live scores and progress `x/n`; classic sync mode stays unchanged.

**Architecture:** Extend `GameSession` with `AsyncRaceEnabled` and per-player `PlayerRaceState` (order, index, endsAt, finished). Hub branches CreateRoom/Start/Submit/PowerUp/timeout on that flag. Host UI shows `AsyncScoreboard`; players reuse the question screen without waiting on the host between questions.

**Tech Stack:** C# / ASP.NET Core SignalR, in-memory `GameSession`, host.js / player.js, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-13-async-race-mode-design.md`

## Global Constraints

- Separate mode only — do not remove or break the sync path.
- Wire flag: `asyncRace: { enabled: bool }` on CreateRoom.
- MVP: Async not combinable with Exam or Auto-Advance (reject/disable).
- Host sees nickname, score, `answeredCount`/`total` — never question text during race.
- Race ends automatically when every joined player has `Finished == true`.
- All player power-ups including disrupt (targets players with an open question).
- MVP: no host arena events required for async race (player power-ups + starters OK).
- Branch work from `main` (or continue `cursor/async-race-mode-design-2102` / rename to `cursor/async-race-mode-2102`).

---

## File map

| File | Role |
|------|------|
| `src/TrainArena/Game/DemoQuestion.cs` | Add `GamePhase.AsyncRacing` |
| `src/TrainArena/Game/PlayerRaceState.cs` | **Create** — per-player race progress |
| `src/TrainArena/Game/GameSession.cs` | AsyncRace flag, StartAsyncRace, submit/timeout/advance, scoreboard, disrupt targets |
| `src/TrainArena/Game/GameSessionStore.cs` | Pass `asyncRace` into ctor |
| `src/TrainArena/Contracts/HubMessages.cs` | `AsyncRaceConfigDto`, `AsyncScoreboardMessage` (+ entry DTO) |
| `src/TrainArena/Hubs/GameHub.cs` | CreateRoom param, start/submit/powerup/timeout/reconnect branches |
| `src/TrainArena/Pages/Index.cshtml` + `wwwroot/js/host.js` | Checkbox + scoreboard UI |
| `src/TrainArena/wwwroot/player/player.js` | Handle scoreboard-less flow; disrupt picker filter; no wait-for-host between Qs |
| `tests/TrainArena.Tests/AsyncRaceSessionTests.cs` | **Create** — session unit tests |
| `tests/TrainArena.Tests/AsyncRaceHubTests.cs` | **Create** — hub integration tests |
| `README.md` | Short mode description |

---

### Task 1: DTOs, phase enum, CreateRoom flag (server reject combo)

**Files:**
- Modify: `src/TrainArena/Game/DemoQuestion.cs` (`GamePhase`)
- Modify: `src/TrainArena/Contracts/HubMessages.cs`
- Modify: `src/TrainArena/Game/GameSession.cs` (ctor + `AsyncRaceEnabled` property)
- Modify: `src/TrainArena/Game/GameSessionStore.cs`
- Modify: `src/TrainArena/Hubs/GameHub.cs` (`CreateRoom` signature + validation)
- Test: `tests/TrainArena.Tests/AsyncRaceSessionTests.cs` (create file; first tests)

**Interfaces:**
- Consumes: existing `CreateRoom(quizId, powerUpConfig, autoAdvance, exam)`
- Produces: `CreateRoom(..., ExamConfigDto? exam, AsyncRaceConfigDto? asyncRace)`  
  `record AsyncRaceConfigDto(bool Enabled);`  
  `GameSession.AsyncRaceEnabled`  
  `GamePhase.AsyncRacing`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/TrainArena.Tests/AsyncRaceSessionTests.cs
using TrainArena.Game;

namespace TrainArena.Tests;

public class AsyncRaceSessionTests
{
    [Fact]
    public void Constructor_SetsAsyncRaceEnabled()
    {
        var s = new GameSession("ABC123", "host", Guid.NewGuid(), asyncRaceEnabled: true);
        Assert.True(s.AsyncRaceEnabled);
        Assert.Equal(GamePhase.Lobby, s.Phase);
    }
}
```

Add hub test file stub or test CreateRoom rejection via existing hub test patterns — prefer a pure validation helper if easier:

In `GameHub.CreateRoom`, after parsing flags:

```csharp
var asyncRace = asyncRace?.Enabled == true;
if (asyncRace && examMode)
{
    await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage(
        "Async-Rennen und Prüfungsmodus sind nicht kombinierbar."));
    return;
}
if (asyncRace && autoAdvanceOptions is { Enabled: true })
{
    await Clients.Caller.SendAsync("JoinError", new JoinErrorMessage(
        "Async-Rennen und Auto-Advance sind nicht kombinierbar."));
    return;
}
```

- [ ] **Step 2: Run test — expect FAIL** (missing ctor param / property)

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~AsyncRaceSessionTests --nologo
```

- [ ] **Step 3: Implement minimal**

1. `GamePhase.AsyncRacing` after `Lobby` (or before `Finished`).
2. `AsyncRaceConfigDto(bool Enabled)` in HubMessages.
3. `GameSession` ctor add `bool asyncRaceEnabled = false`, set `AsyncRaceEnabled = asyncRaceEnabled` (if exam+async both true at ctor, prefer throw or force async false — hub should prevent).
4. When `asyncRaceEnabled`, force `AutoAdvance` disabled copy if needed.
5. `GameSessionStore.Create(..., bool examMode = false, bool asyncRaceEnabled = false)`.
6. `CreateRoom` add `AsyncRaceConfigDto? asyncRace` parameter last; wire validation; pass to Create.

- [ ] **Step 4: Tests PASS + sync smoke still green**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter "FullyQualifiedName~AsyncRaceSessionTests|FullyQualifiedName~GameSessionTests|FullyQualifiedName~Smoke" --nologo
```

- [ ] **Step 5: Commit**

```bash
git add src/TrainArena/Game/DemoQuestion.cs src/TrainArena/Game/GameSession.cs src/TrainArena/Game/GameSessionStore.cs src/TrainArena/Contracts/HubMessages.cs src/TrainArena/Hubs/GameHub.cs tests/TrainArena.Tests/AsyncRaceSessionTests.cs
git commit -m "feat(async-race): CreateRoom flag and AsyncRacing phase"
```

---

### Task 2: PlayerRaceState + StartAsyncRace + scoreboard snapshot

**Files:**
- Create: `src/TrainArena/Game/PlayerRaceState.cs`
- Modify: `src/TrainArena/Game/GameSession.cs`
- Modify: `src/TrainArena/Contracts/HubMessages.cs` (scoreboard DTOs)
- Test: `tests/TrainArena.Tests/AsyncRaceSessionTests.cs`

**Interfaces:**
- Produces:
```csharp
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

public sealed record AsyncScoreboardEntryDto(string Nickname, int Score, int AnsweredCount, int Total);
public sealed record AsyncScoreboardMessage(IReadOnlyList<AsyncScoreboardEntryDto> Entries);

// GameSession:
public (bool ok, string? error) StartAsyncRace(DateTimeOffset nowUtc);
public IReadOnlyList<AsyncScoreboardEntryDto> GetAsyncScoreboard();
public PlayerRaceState? GetRaceState(string nickname);
```

- [ ] **Step 1: Failing tests**

```csharp
[Fact]
public void StartAsyncRace_ShufflesPerPlayer_AndSetsPhase()
{
    var s = new GameSession("ABC123", "host", Guid.NewGuid(), asyncRaceEnabled: true);
    s.SetQuestions(Enumerable.Range(0, 5).Select(i => new DemoQuestion
    {
        Text = $"Q{i}", Options = ["a", "b"], CorrectIndex = 0, TimeLimitSeconds = 20
    }).ToList());
    s.TryJoin("Ada", "c1");
    s.TryJoin("Bob", "c2");

    Assert.True(s.StartAsyncRace(DateTimeOffset.UtcNow).ok);
    Assert.Equal(GamePhase.AsyncRacing, s.Phase);

    var ada = s.GetRaceState("Ada")!;
    var bob = s.GetRaceState("Bob")!;
    Assert.Equal(5, ada.Order.Count);
    Assert.Equal(0, ada.Index);
    Assert.NotNull(ada.Current);
    // Not required that shuffles differ every run; assert both have same multiset of texts:
    Assert.Equal(
        ada.Order.Select(q => q.Text).OrderBy(x => x),
        bob.Order.Select(q => q.Text).OrderBy(x => x));
}

[Fact]
public void StartAsyncRace_RejectedWhenNotAsyncMode()
{
    var s = new GameSession("ABC123", "host", Guid.NewGuid(), asyncRaceEnabled: false);
    s.SetQuestions([new DemoQuestion { Text = "Q", Options = ["a", "b"], CorrectIndex = 0 }]);
    s.TryJoin("Ada", "c1");
    Assert.False(s.StartAsyncRace(DateTimeOffset.UtcNow).ok);
}

[Fact]
public void GetAsyncScoreboard_ShowsZeroProgress()
{
    var s = new GameSession("ABC123", "host", Guid.NewGuid(), asyncRaceEnabled: true);
    s.SetQuestions([new DemoQuestion { Text = "Q", Options = ["a", "b"], CorrectIndex = 0 }]);
    s.TryJoin("Ada", "c1");
    Assert.True(s.StartAsyncRace(DateTimeOffset.UtcNow).ok);
    var board = s.GetAsyncScoreboard();
    Assert.Single(board);
    Assert.Equal(0, board[0].AnsweredCount);
    Assert.Equal(1, board[0].Total);
}
```

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement**

```csharp
// PlayerRaceState.cs — as in Interfaces

// Inside GameSession:
private readonly Dictionary<string, PlayerRaceState> _races =
    new(StringComparer.OrdinalIgnoreCase);

public bool AsyncRaceEnabled { get; }

static List<T> ShuffleCopy<T>(IReadOnlyList<T> source, Random rng)
{
    var list = source.ToList();
    for (var i = list.Count - 1; i > 0; i--)
    {
        var j = rng.Next(i + 1);
        (list[i], list[j]) = (list[j], list[i]);
    }
    return list;
}

public (bool ok, string? error) StartAsyncRace(DateTimeOffset nowUtc)
{
    lock (_gate)
    {
        if (!AsyncRaceEnabled) return (false, "Kein Async-Modus");
        if (Phase != GamePhase.Lobby) return (false, "Ungültige Phase");
        if (_quizQuestions.Count == 0) return (false, "Keine Fragen");
        if (_players.Count == 0) return (false, "Keine Spieler");

        var rng = Random.Shared;
        _races.Clear();
        foreach (var p in _players)
        {
            var order = ShuffleCopy(_quizQuestions, rng);
            var q0 = order[0];
            var limit = TimeSpan.FromSeconds(q0.TimeLimitSeconds <= 0 ? 20 : q0.TimeLimitSeconds);
            _races[p.Nickname] = new PlayerRaceState
            {
                Order = order,
                Index = 0,
                QuestionStartedAtUtc = nowUtc,
                EndsAtUtc = nowUtc + limit,
            };
            // ensure power-up state exists if PowerUpConfig.Enabled (reuse existing join init if any)
        }

        Phase = GamePhase.AsyncRacing;
        return (true, null);
    }
}
```

`GetAsyncScoreboard`: lock; for each player join race state; build DTOs; sort by Score desc, Nickname asc.

- [ ] **Step 4: PASS + commit**

```bash
git add src/TrainArena/Game/PlayerRaceState.cs src/TrainArena/Game/GameSession.cs src/TrainArena/Contracts/HubMessages.cs tests/TrainArena.Tests/AsyncRaceSessionTests.cs
git commit -m "feat(async-race): per-player shuffle and scoreboard snapshot"
```

---

### Task 3: Submit answer, personal advance, finish-all

**Files:**
- Modify: `src/TrainArena/Game/GameSession.cs`
- Test: `tests/TrainArena.Tests/AsyncRaceSessionTests.cs`

**Interfaces:**
```csharp
public sealed record AsyncSubmitResult(
    bool Ok,
    string? Error,
    int PointsAwarded,
    bool Correct,
    int CorrectIndex,
    string? Explanation,
    bool PlayerFinished,
    bool RaceFinished,
    DemoQuestion? NextQuestion,
    DateTimeOffset? NextStartedAtUtc,
    DateTimeOffset? NextEndsAtUtc,
    int NextIndex,
    int Total);

public AsyncSubmitResult SubmitAsyncAnswer(string nickname, int optionIndex, DateTimeOffset nowUtc);
public (bool timedOut, AsyncSubmitResult? result) TryTimeoutAsyncPlayer(string nickname, DateTimeOffset nowUtc);
```

- [ ] **Step 1: Failing tests**

```csharp
[Fact]
public void SubmitAsyncAnswer_AdvancesOnlyThatPlayer()
{
    var s = NewAsyncTwoPlayerTwoQuestions();
    Assert.True(s.StartAsyncRace(DateTimeOffset.UtcNow).ok);
    var adaQ = s.GetRaceState("Ada")!.Current!;
    var r = s.SubmitAsyncAnswer("Ada", adaQ.CorrectIndex, DateTimeOffset.UtcNow);
    Assert.True(r.Ok);
    Assert.Equal(1, s.GetRaceState("Ada")!.Index);
    Assert.Equal(0, s.GetRaceState("Bob")!.Index);
    Assert.False(r.RaceFinished);
}

[Fact]
public void AllPlayersFinished_SetsPhaseFinished()
{
    var s = NewAsyncOnePlayerOneQuestion();
    Assert.True(s.StartAsyncRace(DateTimeOffset.UtcNow).ok);
    var q = s.GetRaceState("Ada")!.Current!;
    var r = s.SubmitAsyncAnswer("Ada", q.CorrectIndex, DateTimeOffset.UtcNow);
    Assert.True(r.PlayerFinished);
    Assert.True(r.RaceFinished);
    Assert.Equal(GamePhase.Finished, s.Phase);
}
```

Helper factories build session + joins + questions.

Scoring: reuse existing point rules as much as possible (read `SubmitAnswer` in GameSession and extract shared private helper if needed — prefer call into shared scoring without breaking sync).

- [ ] **Step 2: FAIL → Step 3 implement → Step 4 PASS**

On correct/wrong: increment `Index`; if not finished, set new `EndsAt` from next question duration; clear per-question disrupt flags for that player (`PlayerPowerUpState.DisruptedThisQuestion` if exists).

`TryTimeoutAsyncPlayer`: if `nowUtc >= EndsAt` and not finished and no answer for current → award 0 / mark wrong, advance like submit.

- [ ] **Step 5: Commit**

```bash
git commit -am "feat(async-race): personal submit, advance, and race finish"
```

---

### Task 4: Power-ups in async (incl. disrupt targets)

**Files:**
- Modify: `src/TrainArena/Game/GameSession.cs` (`TryUsePowerUp` branch)
- Test: `tests/TrainArena.Tests/AsyncRaceSessionTests.cs` (+ existing PowerUp tests must stay green)

**Interfaces:**
- Consumes: existing `TryUsePowerUp(nickname, powerUpId, targetNickname, nowUtc)`
- Produces: when `AsyncRaceEnabled && Phase == AsyncRacing`:
  - Extra time extends **race** `EndsAtUtc`
  - 50/50 masks **race** current options
  - Disrupt: valid targets = other players where `GetRaceState(n) is { Finished: false, Current: not null }` and not yet answered current (always true until submit — use “not finished and has Current”)
  - Apply disrupt flag on target’s power-up state for *their* current question

- [ ] **Step 1: Tests**

```csharp
[Fact]
public void Disrupt_RejectsFinishedTarget()
{
    // Ada finishes 1-question race; Bob still racing (2 questions) — disrupt Ada fails
}

[Fact]
public void ExtraTime_ExtendsAsyncEndsAt()
{
    // grant inventory, use extra_time, assert EndsAtUtc increased
}
```

- [ ] **Step 2–4: TDD implement carefully by reading sync `TryUsePowerUp` and branching on `AsyncRaceEnabled`**

- [ ] **Step 5: Commit**

```bash
git commit -am "feat(async-race): power-ups and disrupt against active racers"
```

---

### Task 5: GameHub wiring (start, submit, timeout loop, reconnect, scoreboard)

**Files:**
- Modify: `src/TrainArena/Hubs/GameHub.cs`
- Create: `tests/TrainArena.Tests/AsyncRaceHubTests.cs`

**Interfaces:**
- Host start path: if `session.AsyncRaceEnabled` → `StartAsyncRace` instead of first sync `StartQuestion`.
- For each player: `Clients.Client(connectionId).SendAsync("QuestionStarted", ...)` with **per-player** index/total from race state (`Index+1` display vs 0-based — match existing QuestionStarted `Index` semantics used by player.js).
- Broadcast `AsyncScoreboard` to group or host.
- `SubmitAnswer`: if async racing → `SubmitAsyncAnswer`; send personal `QuestionEnded` (or small feedback) + next `QuestionStarted` or wait for finish; on `RaceFinished` send `GameFinished` (review may be empty or minimal in MVP — prefer empty Review list + leaderboard entries from scores).
- Background timeout: reuse/extend existing question-end timer mechanism — for async, periodically check all race states OR schedule per-player delays. **MVP approach:** on each Submit and on a single hub timer tick (if one exists), call `TryTimeoutAsyncPlayer` for all. Inspect how sync timeout is scheduled in GameHub and mirror with a loop over nicknames.

- [ ] **Step 1: Hub test** — create room with asyncRace, two players, host starts, both receive QuestionStarted (possibly different texts), one answers, scoreboard updates.

Follow patterns in `FullRoundTests` / `PowerUpHubTests` / `ExamModeTests` for `HubConnection` setup.

- [ ] **Step 2: FAIL → implement hub branches → PASS**

- [ ] **Step 3: Commit**

```bash
git commit -am "feat(async-race): hub start, submit, timeout, scoreboard"
```

---

### Task 6: Host UI

**Files:**
- Modify: `src/TrainArena/Pages/Index.cshtml` (checkbox near exam/auto-advance)
- Modify: `src/TrainArena/wwwroot/js/host.js`
- Modify: `src/TrainArena/wwwroot/css/host.css` (minimal scoreboard row styles if needed)

**Interfaces:**
- Checkbox `id="async-race-enabled"`
- `readAsyncRaceConfig()` → `{ enabled: bool }`
- `CreateRoom(..., exam, asyncRace)` — update invoke arity to match hub
- `syncAsyncRaceUi()`: if async on, uncheck/disable exam + auto-advance (and vice versa)
- `connection.on("AsyncScoreboard", renderAsyncScoreboard)`
- During async race hide next-question controls; show `#async-scoreboard` list: `nick — score — x/n`
- Start button label can stay or become „Rennen starten“ when async checked

- [ ] **Step 1: Implement UI + wire events** (no separate unit test; smoke by loading host page string contains `async-race-enabled` — optional small test)

Optional test:

```csharp
[Fact]
public async Task HostPage_HasAsyncRaceCheckbox()
{
    using var f = new TrainArenaWebAppFactory();
    var html = await f.CreateClient().GetStringAsync("/");
    Assert.Contains("async-race-enabled", html);
}
```

- [ ] **Step 2: Commit**

```bash
git commit -am "feat(async-race): host create toggle and live scoreboard"
```

---

### Task 7: Player UI

**Files:**
- Modify: `src/TrainArena/wwwroot/player/player.js`

**Behavior:**
- On `QuestionEnded` in async race: show brief feedback (existing explanation path OK), then expect next `QuestionStarted` without requiring Leaderboard/wait-host. If `wait` panel would show, skip when next question arrives quickly.
- Disrupt target list: prefer server-provided list if you add `DisruptTargets` message; **MVP:** keep client invoke with nickname; server validates — update picker to request targets via new hub method `GetAsyncDisruptTargets` returning nicknames **or** embed eligible targets in `AsyncScoreboard` / inventory push. Simplest: hub method `ListAsyncDisruptTargets` → `string[]`.
- When `GameFinished` arrives, show board as today.

- [ ] **Step 1: Implement player.js changes + optional tiny hub list method**

- [ ] **Step 2: Manual note in commit; run PowerUp + AsyncRace tests**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter "FullyQualifiedName~AsyncRace|FullyQualifiedName~PowerUp|FullyQualifiedName~FullRound|FullyQualifiedName~Smoke" --nologo
```

- [ ] **Step 3: Commit**

```bash
git commit -am "feat(async-race): player flow without host wait between questions"
```

---

### Task 8: README + spec status + PR

**Files:**
- Modify: `README.md` (features: Async-Rennen bullet)
- Modify: `docs/superpowers/specs/2026-10-13-async-race-mode-design.md` → Status Implemented/Approved
- Push branch, open PR to `main`

- [ ] **Step 1: Docs**
- [ ] **Step 2: Full test suite**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --nologo
```

- [ ] **Step 3: Commit, push, PR**

```bash
git add README.md docs/superpowers/specs/2026-10-13-async-race-mode-design.md
git commit -m "docs: Async-Rennen mode in README and spec status"
git push -u origin HEAD
```

---

## Spec coverage checklist

| Spec item | Task |
|-----------|------|
| CreateRoom `asyncRace` + no Exam/Auto-Advance combo | 1, 6 |
| PlayerRaceState + shuffle | 2 |
| Personal Q/timer/submit/advance | 3, 5, 7 |
| Auto finish all done | 3, 5 |
| Host scoreboard x/n | 2, 5, 6 |
| Power-ups + disrupt active targets | 4, 5, 7 |
| Sync unchanged | 1–8 regression filters |
| README | 8 |
