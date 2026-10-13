# Player Question UI Polish (Meta + Power-Ups) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Visually polish the player question screen meta bar (progress + timer + rest-time track) and power-up chips without changing game flows.

**Architecture:** CSS-first updates in `player.css`, one markup hook in `index.html` for the timer track, and small `player.js` changes: drive `--timer-pct` from the existing timer tick, and render power-up count as a badge. No hub/server changes.

**Tech Stack:** Static player UI (`index.html`, `player.css`, `player.js`), ASP.NET Core static files, xUnit smoke tests via `WebApplicationFactory`.

**Spec:** `docs/superpowers/specs/2026-10-13-player-question-ui-polish-design.md`

## Global Constraints

- Amber player theme stays (`--accent: #fbbf24`); do not switch to host cyan.
- Join / wait / board / answer-button behavior unchanged.
- No new power-up IDs, hub messages, or inventory rules.
- Reuse existing timer tick (`startedAt` / `endsAt`); no second interval.
- Respect `prefers-reduced-motion` for critical timer pulse (track stays static).
- Branch: `cursor/player-question-ui-polish-2102` (already has the design spec commit).

---

## File map

| File | Role |
|------|------|
| `src/TrainArena/wwwroot/player/index.html` | Add `#q-timer-track` under `.q-meta` |
| `src/TrainArena/wwwroot/player/player.css` | Meta/track styles; chip power-up layout + badge |
| `src/TrainArena/wwwroot/player/player.js` | Set `--timer-pct` + urgency classes on track; badge markup in `ensurePowerUpButtons` |
| `tests/TrainArena.Tests/PlayerUiSmokeTests.cs` | Create: HTTP smoke that player HTML/CSS contain track + chip hooks |

---

### Task 1: Smoke tests for player markup/CSS hooks

**Files:**
- Create: `tests/TrainArena.Tests/PlayerUiSmokeTests.cs`
- Test: same file

**Interfaces:**
- Consumes: `TrainArenaWebAppFactory`, static `/player/` and `/player/player.css`
- Produces: failing tests until Tasks 2–3 land

- [ ] **Step 1: Write failing smoke tests**

Create `tests/TrainArena.Tests/PlayerUiSmokeTests.cs`:

```csharp
using System.Net;

namespace TrainArena.Tests;

public class PlayerUiSmokeTests : IClassFixture<TrainArenaWebAppFactory>
{
    private readonly HttpClient _client;

    public PlayerUiSmokeTests(TrainArenaWebAppFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task PlayerIndex_HasTimerTrackHook()
    {
        var res = await _client.GetAsync("/player/");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("id=\"q-timer-track\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"q-meta\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"powerups\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlayerCss_DefinesTimerTrackAndPowerupBadge()
    {
        var res = await _client.GetAsync("/player/player.css");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var css = await res.Content.ReadAsStringAsync();
        Assert.Contains(".q-timer-track", css, StringComparison.Ordinal);
        Assert.Contains("--timer-pct", css, StringComparison.Ordinal);
        Assert.Contains(".powerups button .count", css, StringComparison.Ordinal);
        Assert.Contains("border-radius: 999px", css, StringComparison.Ordinal); // chip/badge shape signal
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~PlayerUiSmokeTests --nologo
```

Expected: FAIL — missing `q-timer-track` / `.q-timer-track` / chip radius signal.

- [ ] **Step 3: Commit tests**

```bash
git add tests/TrainArena.Tests/PlayerUiSmokeTests.cs
git commit -m "test: smoke hooks for player timer track and power-up chips"
```

---

### Task 2: Timer track markup + CSS + JS pct

**Files:**
- Modify: `src/TrainArena/wwwroot/player/index.html` (question section ~lines 37–48)
- Modify: `src/TrainArena/wwwroot/player/player.css` (`.q-meta` / `.timer` area ~177–206)
- Modify: `src/TrainArena/wwwroot/player/player.js` (`setTimerUrgency`, `startTimer` ~610–638)

**Interfaces:**
- Consumes: existing `startedAt`, `endsAt`, `questionOpen`, `qTimer`
- Produces: `#q-timer-track` with CSS var `--timer-pct` (0–100%) and urgency classes mirrored from timer

- [ ] **Step 1: Add track element in HTML**

In `index.html`, inside `#question` after `.q-meta` and before `#q-text`:

```html
      <div class="q-meta">
        <p id="q-progress" class="muted"></p>
        <p id="q-timer" class="timer" aria-live="polite"></p>
      </div>
      <div id="q-timer-track" class="q-timer-track" aria-hidden="true">
        <div class="q-timer-track-fill"></div>
      </div>
      <div id="q-text" class="q-text" role="heading" aria-level="2"></div>
```

- [ ] **Step 2: Add track CSS**

In `player.css` after `.timer.is-critical` block, add:

```css
.q-timer-track {
  --timer-pct: 100%;
  height: 4px;
  margin: 0.35rem 0 0.55rem;
  border-radius: 999px;
  background: color-mix(in srgb, var(--panel-border) 70%, transparent);
  overflow: hidden;
}
.q-timer-track-fill {
  height: 100%;
  width: var(--timer-pct);
  border-radius: inherit;
  background: var(--accent);
  transition: width 180ms linear;
}
.q-timer-track.is-urgent .q-timer-track-fill { background: #fb923c; }
.q-timer-track.is-critical .q-timer-track-fill { background: var(--danger); }
@media (prefers-reduced-motion: reduce) {
  .q-timer-track-fill { transition: none; }
  .timer.is-critical { animation: none; }
}
```

(If a later `@media (prefers-reduced-motion)` already clears `.timer.is-critical`, merge rather than duplicate.)

- [ ] **Step 3: Drive `--timer-pct` from existing tick**

Near top of `player.js` IIFE (with other `$()` lookups):

```javascript
  const qTimerTrack = $("q-timer-track");
```

Update `setTimerUrgency` and `startTimer` tick:

```javascript
  function setTimerUrgency(msLeft) {
    qTimer.classList.remove("is-urgent", "is-critical");
    if (qTimerTrack) qTimerTrack.classList.remove("is-urgent", "is-critical");
    if (!questionOpen || msLeft <= 0) return;
    const sec = Math.ceil(msLeft / 1000);
    if (sec <= 5) {
      qTimer.classList.add("is-critical");
      if (qTimerTrack) qTimerTrack.classList.add("is-critical");
    } else if (sec <= 10) {
      qTimer.classList.add("is-urgent");
      if (qTimerTrack) qTimerTrack.classList.add("is-urgent");
    }
  }

  function updateTimerTrack(msLeft) {
    if (!qTimerTrack || !endsAt || !startedAt) return;
    const total = endsAt - startedAt;
    if (total <= 0) {
      qTimerTrack.style.setProperty("--timer-pct", "0%");
      return;
    }
    const pct = Math.max(0, Math.min(100, (msLeft / total) * 100));
    qTimerTrack.style.setProperty("--timer-pct", `${pct}%`);
  }

  function startTimer() {
    stopTimer();
    const tick = () => {
      if (!endsAt || !startedAt) return;
      const ms = endsAt - Date.now();
      if (!questionOpen) {
        qTimer.textContent = "0s";
        qTimer.classList.remove("is-urgent", "is-critical");
        if (qTimerTrack) {
          qTimerTrack.classList.remove("is-urgent", "is-critical");
          qTimerTrack.style.setProperty("--timer-pct", "0%");
        }
        return;
      }
      if (ms <= 0) {
        qTimer.textContent = "0s · warte auf Server…";
        qTimer.classList.add("is-critical");
        if (qTimerTrack) {
          qTimerTrack.classList.add("is-critical");
          qTimerTrack.style.setProperty("--timer-pct", "0%");
        }
        return;
      }
      qTimer.textContent = `${Math.ceil(ms / 1000)}s`;
      setTimerUrgency(ms);
      updateTimerTrack(ms);
    };
    tick();
    timerHandle = setInterval(tick, 200);
  }
```

- [ ] **Step 4: Run smoke test for HTML hook**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~PlayerIndex_HasTimerTrackHook --nologo
```

Expected: PASS for HTML test; CSS test may still fail until Task 3 if chip radius not yet present — either finish Task 3 next or temporarily satisfy both CSS asserts in this task by including chip radius in powerups CSS early. Prefer completing Task 3 immediately after.

- [ ] **Step 5: Commit**

```bash
git add src/TrainArena/wwwroot/player/index.html src/TrainArena/wwwroot/player/player.css src/TrainArena/wwwroot/player/player.js
git commit -m "feat(player): timer rest-time track on question meta"
```

---

### Task 3: Power-up chip layout + count badge

**Files:**
- Modify: `src/TrainArena/wwwroot/player/player.css` (`.powerups` block ~341–393)
- Modify: `src/TrainArena/wwwroot/player/player.js` (`ensurePowerUpButtons`, inventory count updater that sets `.count` text)

**Interfaces:**
- Consumes: `PLAYER_POWERUPS`, existing `refreshPowerUpButtons` / count logic
- Produces: buttons with `<span class="count">n</span>` badge; chip CSS

- [ ] **Step 1: Update button markup**

In `ensurePowerUpButtons`:

```javascript
      btn.innerHTML = `<span class="pu-label">${p.label}</span><span class="count">0</span>`;
```

Find where count text is set (search for `.count` / `textContent` with parentheses). Change to set badge text to the number only, e.g. `${n}` not ` (${n})`.

Example pattern to apply wherever counts are written:

```javascript
      const countEl = btn.querySelector(".count");
      if (countEl) countEl.textContent = String(n);
```

- [ ] **Step 2: Chip CSS**

Replace/extend `.powerups` / `.powerups button` / `.powerups button .count` with:

```css
.powerups {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  margin: 0.65rem 0 0.45rem;
}
.powerups button {
  width: auto;
  flex: 0 1 auto;
  margin-top: 0;
  min-height: 2.6rem;
  padding: 0.45rem 0.75rem;
  font-size: 0.88rem;
  font-weight: 650;
  background: #334155;
  color: var(--text);
  border: 1px solid var(--panel-border);
  border-radius: 999px;
  position: relative;
  overflow: hidden;
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
}
.powerups button .pu-label { line-height: 1.2; }
.powerups button .count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 1.35rem;
  height: 1.35rem;
  padding: 0 0.35rem;
  border-radius: 999px;
  font-size: 0.75rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  background: color-mix(in srgb, var(--input-bg) 80%, black);
  color: var(--text);
  opacity: 1;
}
```

Keep existing `is-active` / shield / double / `is-activating` / consume rules; only adjust if border-radius on active state needs `999px` consistency.

- [ ] **Step 3: Run all player UI smokes**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~PlayerUiSmokeTests --nologo
```

Expected: PASS both tests.

- [ ] **Step 4: Run related power-up / hub tests (regression)**

```bash
dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter "FullyQualifiedName~PowerUp|FullyQualifiedName~FullRound|FullyQualifiedName~Smoke" --nologo
```

Expected: PASS (no logic change).

- [ ] **Step 5: Commit**

```bash
git add src/TrainArena/wwwroot/player/player.css src/TrainArena/wwwroot/player/player.js
git commit -m "feat(player): chip-style power-ups with count badges"
```

---

### Task 4: Manual check + PR

**Files:**
- Modify: `docs/superpowers/specs/2026-10-13-player-question-ui-polish-design.md` (status → Approved/Implemented when done)

- [ ] **Step 1: Manual walkthrough**

Start app, join as player, run a question with power-ups enabled:

1. Meta shows progress + timer; track shrinks with time; urgent/critical colors match.
2. Power-ups appear as chips with numeric badges; active/disabled/consume still work.
3. Extra-Zeit extends timer and track; answers still selectable.
4. With OS reduced-motion on: no critical pulse; track has no width transition.

Save screenshots under `/opt/cursor/artifacts/` (meta+track, power-up chips).

- [ ] **Step 2: Mark spec status**

In the design doc header, set `**Status:** Approved` (or `Implemented` after ship).

- [ ] **Step 3: Push and open/update PR**

```bash
git push -u origin cursor/player-question-ui-polish-2102
```

PR base: `main`. Title e.g. `polish(player): Frage-Meta-Timer-Track und Power-Up-Chips`.

- [ ] **Step 4: Final commit if status/docs only**

```bash
git add docs/superpowers/specs/2026-10-13-player-question-ui-polish-design.md
git commit -m "docs: mark player question UI polish spec approved"
git push -u origin cursor/player-question-ui-polish-2102
```

---

## Spec coverage checklist

| Spec item | Task |
|-----------|------|
| Meta layout progress + timer | Task 2 |
| Timer track + pct from same tick | Task 2 |
| urgent/critical colors on track | Task 2 |
| reduced-motion | Task 2 |
| Chip power-ups + badge | Task 3 |
| Visibility rules unchanged | Task 3 (no visibility logic change) |
| Answers / picker unchanged | Tasks 2–3 (no edits) |
| Smoke + manual abnahme | Tasks 1, 4 |
