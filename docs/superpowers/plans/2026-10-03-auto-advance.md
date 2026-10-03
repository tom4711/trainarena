# Auto-Advance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Optional server-driven advance after Leaderboard (3/5/10s), default off; Host Next cancels.

**Architecture:** Store flags on `GameSession`; `GameHub` schedules a CTS delay after Leaderboard broadcast and runs the same transition as `NextQuestion` on fire; cancel on manual Next / teardown.

**Tech Stack:** ASP.NET Core net10.0, SignalR, xUnit, existing host/player JS.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-10-03-auto-advance-design.md`
- Default: `AutoAdvanceEnabled=false`, `AutoAdvanceDelaySeconds=5`
- Delay allow-list only: `3`, `5`, `10`
- Advance only from `GamePhase.Leaderboard` after board is shown
- Host `NextQuestion` always cancels pending auto-advance (no double advance)
- German UI; keep Host Next as primary path
- TDD; branch `cursor/auto-advance-47f9`; CI must stay green

---

## File map

| File | Role |
|------|------|
| `src/TrainArena/Game/AutoAdvanceOptions.cs` | Options + `CreateDefault()` + `Validate()` |
| `src/TrainArena/Game/GameSession.cs` | Hold options |
| `src/TrainArena/Game/GameSessionStore.cs` | Pass options into `Create` |
| `src/TrainArena/Contracts/HubMessages.cs` | DTO + schedule/cancel messages |
| `src/TrainArena/Hubs/GameHub.cs` | CreateRoom wiring, schedule/cancel, fire = Next logic |
| `src/TrainArena/Pages/Index.cshtml` + `wwwroot/js/host.js` | Lobby controls + board countdown |
| `src/TrainArena/wwwroot/player/*` | Board countdown |
| `README.md` | One-line note |
| `tests/TrainArena.Tests/AutoAdvance*.cs` | Config + hub/session tests |

---

### Task 1: Options + session wiring

**Files:**
- Create: `src/TrainArena/Game/AutoAdvanceOptions.cs`
- Modify: `GameSession.cs` ctor / property
- Modify: `GameSessionStore.Create` to accept `AutoAdvanceOptions?`
- Test: `tests/TrainArena.Tests/AutoAdvanceOptionsTests.cs`

**Interfaces:**
- Produces: `AutoAdvanceOptions.CreateDefault()`, `Validate() → string?`, `GameSession.AutoAdvance`

- [ ] **Step 1: Failing tests**

```csharp
public class AutoAdvanceOptionsTests
{
    [Fact]
    public void CreateDefault_OffWithFiveSeconds()
    {
        var o = AutoAdvanceOptions.CreateDefault();
        Assert.False(o.Enabled);
        Assert.Equal(5, o.DelaySeconds);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void Validate_AcceptsAllowList(int delay)
    {
        var o = new AutoAdvanceOptions { Enabled = true, DelaySeconds = delay };
        Assert.Null(o.Validate());
    }

    [Fact]
    public void Validate_RejectsOtherDelay()
    {
        var o = new AutoAdvanceOptions { Enabled = true, DelaySeconds = 7 };
        Assert.NotNull(o.Validate());
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~AutoAdvanceOptionsTests --nologo
```

- [ ] **Step 3: Implement `AutoAdvanceOptions` + session property**

```csharp
namespace TrainArena.Game;
public sealed class AutoAdvanceOptions
{
    public static readonly int[] AllowedDelays = [3, 5, 10];
    public bool Enabled { get; set; }
    public int DelaySeconds { get; set; } = 5;
    public static AutoAdvanceOptions CreateDefault() => new() { Enabled = false, DelaySeconds = 5 };
    public string? Validate() =>
        AllowedDelays.Contains(DelaySeconds) ? null : "AutoAdvanceDelaySeconds muss 3, 5 oder 10 sein.";
}
```

`GameSession`: add `AutoAdvanceOptions AutoAdvance { get; }` set in ctor (default `CreateDefault()`). Extend ctor/`Store.Create` with optional param without breaking PowerUp config overload — e.g. `Create(..., PowerUpRoomConfig? powerUps = null, AutoAdvanceOptions? autoAdvance = null)`.

- [ ] **Step 4: Full suite PASS**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --nologo
```

- [ ] **Step 5: Commit** `feat: auto-advance room options`

---

### Task 2: Hub schedule / cancel / fire

**Files:**
- Modify: `HubMessages.cs` — `AutoAdvanceConfigDto`, `AutoAdvanceScheduledMessage`, `AutoAdvanceCancelledMessage`
- Modify: `GameHub.cs` — CreateRoom mapping; after Leaderboard in both end pipelines call `ScheduleAutoAdvance`; `NextQuestion` cancels first; fire handler calls shared advance method
- Test: `tests/TrainArena.Tests/AutoAdvanceHubTests.cs`

**Interfaces:**
- Consumes: session `AutoAdvance`
- Produces: SignalR events; no double-fire

- [ ] **Step 1: Add DTOs**

```csharp
public sealed record AutoAdvanceConfigDto(bool Enabled, int DelaySeconds);
public sealed record AutoAdvanceScheduledMessage(DateTimeOffset AdvancesAtUtc);
public sealed record AutoAdvanceCancelledMessage();
```

- [ ] **Step 2: Failing hub test** — create room with `Enabled=true`, `DelaySeconds=3`; join; start; answer; wait for Leaderboard; assert `AutoAdvanceScheduled` received; invoke Host `NextQuestion` before delay; assert `AutoAdvanceCancelled` (or no second QuestionStarted from timer). Prefer: after cancel, wait > delay and assert no extra `QuestionStarted` beyond the manual one.

Simpler unit-style alternative if hub timing is flaky: extract schedule registry testable via package-visible helper — prefer real hub test with `DelaySeconds=3` and `Task.Delay(500)` after cancel.

- [ ] **Step 3: Implement hub**
  - `CreateRoom(Guid quizId, PowerUpConfigDto? powerUpConfig = null, AutoAdvanceConfigDto? autoAdvance = null)` — map/validate auto options; invalid → `JoinError`.
  - Field: `ConcurrentDictionary<string, CancellationTokenSource> _autoAdvanceTimers`.
  - `CancelAutoAdvance(code)` — cancel+dispose CTS; optional broadcast `AutoAdvanceCancelled` when a timer was live.
  - `ScheduleAutoAdvance(session)` — if !Enabled or phase≠Leaderboard return; cancel prior; new CTS; `advancesAtUtc = UtcNow + Delay`; broadcast Scheduled; `Task.Run` delay then if still Leaderboard and token not cancelled → call internal `AdvanceFromLeaderboardAsync(session)` (shared with `NextQuestion` body after phase check).
  - Call `ScheduleAutoAdvance` at end of both Leaderboard broadcast paths (submit timer + hub context).
  - `NextQuestion`: `CancelAutoAdvance` first (broadcast cancel if had timer).
  - Update existing tests that invoke `CreateRoom` with arity (add `null` for third arg like PowerUp second arg).

- [ ] **Step 4: Full suite PASS**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --nologo
```

- [ ] **Step 5: Commit** `feat: server auto-advance after leaderboard`

---

### Task 3: Host/Player UI + README

**Files:**
- Modify: `Pages/Index.cshtml`, `wwwroot/js/host.js`, `player/index.html`, `player.js`, CSS lightly, `README.md`

- [ ] **Step 1: Host lobby** — checkbox „Automatisch weiter“, `<select>` 3/5/10; pass as third arg to `CreateRoom` (or `{ enabled, delaySeconds }` object). SignalR C# optional args: pass `null` for powerups if unchanged structure — current host already passes powerUp config as 2nd; add 3rd.

- [ ] **Step 2: Board countdown** — on `AutoAdvanceScheduled` show hint with remaining seconds from `advancesAtUtc`; clear on `AutoAdvanceCancelled` / `QuestionStarted` / `GameFinished`. Host + Player.

- [ ] **Step 3: README** — one sentence under Host flow / roadmap: optional Auto-Advance after board, default off.

- [ ] **Step 4: `dotnet test` green; smoke `/health`**

- [ ] **Step 5: Commit** `feat: auto-advance host/player UI`

---

## Spec coverage

| Spec item | Task |
|-----------|------|
| Options defaults + allow-list | 1 |
| Session storage | 1 |
| Schedule after Leaderboard | 2 |
| Fire = NextQuestion transition | 2 |
| Host Next cancels | 2 |
| Messages Scheduled/Cancelled | 2 |
| Lobby + countdown UI | 3 |
| README | 3 |

## Self-review

- No TBD left; delay allow-list explicit.  
- CreateRoom gains optional 3rd parameter — update all hub test invokes.  
- Double-advance prevented by Cancel before manual Next + phase check on fire.
