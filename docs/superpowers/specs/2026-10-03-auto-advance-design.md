# TrainArena — Auto-Advance Design

**Status:** Approved for implementation planning (Thomas, 2026-10-03)  
**Branch:** `cursor/auto-advance-47f9`  
**Depends on:** MVP Host Next + PowerUps on `main`

## Goal

Optional **Auto-Advance after Leaderboard**: when enabled, the server advances to the next question (or Finale) after a short delay. **Host Next remains the default path** and always cancels a pending auto-advance.

## Locked decisions

| Topic | Choice |
|-------|--------|
| When | **A** — After Leaderboard (Reveal → Board unchanged), then delay → Next/Finish |
| Opt-in | Toggle **off** by default |
| Delay | **B** — Selectable **3 / 5 / 10** seconds; default **5** |
| Architecture | **1** — Server timer in `GameHub` (CTS per room), same transition as `NextQuestion` |

## Non-goals

- Auto-advance that skips Leaderboard  
- Client-only countdown as source of truth  
- Pause / extend auto-advance mid-countdown (beyond Host Next cancel)  
- Making Auto-Advance mandatory  

---

## §1 Config & flow

### Room options (set at `CreateRoom`)

| Field | Default | Allowed |
|-------|---------|---------|
| `AutoAdvanceEnabled` | `false` | bool |
| `AutoAdvanceDelaySeconds` | `5` | `3`, `5`, or `10` only |

Invalid delay → reject create (or clamp via validation error to caller), same style as PowerUp config validation.

### Flow

1. Question ends → Reveal → Leaderboard (existing Host Next UX).  
2. If `AutoAdvanceEnabled` and phase is Leaderboard: start server delay; broadcast `AutoAdvanceScheduled { advancesAtUtc }`.  
3. On timer fire: run the same transition as `NextQuestion` (next question or `GameFinished`).  
4. If Host invokes `NextQuestion` before fire: cancel CTS; broadcast `AutoAdvanceCancelled` (optional but useful for UI); proceed manually.  
5. Room teardown / host gone: cancel pending auto-advance.

### UI

- Host lobby: checkbox „Automatisch weiter“ + select Delay (3/5/10).  
- Host + Player board: when scheduled, show „Automatisch in …s“ derived from `advancesAtUtc`.  
- Host **Weiter** button always available and cancels the timer.

---

## §2 Architecture

### Session

Store on `GameSession` (or small nested options):

- `bool AutoAdvanceEnabled`  
- `int AutoAdvanceDelaySeconds`  
- Set at create; immutable for the room lifetime (v1).

### Hub

- Accept optional auto-advance fields on create (dedicated small DTO or fields alongside PowerUp config — keep mapping explicit).  
- `Dictionary<string, CancellationTokenSource>` (or reuse pattern from question-end timers) keyed by room code for auto-advance.  
- `ScheduleAutoAdvance(session)` after Leaderboard broadcast when enabled.  
- `CancelAutoAdvance(code)` from `NextQuestion`, disconnect cleanup, and before starting a new question.  
- Messages:
  - `AutoAdvanceScheduled` → `{ advancesAtUtc }`  
  - `AutoAdvanceCancelled` → `{}` or `{ reason }` (optional; empty ok)

### Clients

- Host `CreateRoom` passes config.  
- On `AutoAdvanceScheduled` / `Leaderboard`: start display countdown; clear on `AutoAdvanceCancelled`, `QuestionStarted`, or `GameFinished`.

### Testing

- Validate delay allow-list and defaults.  
- Enabled: after reaching Leaderboard, schedule exists / fires into next question (use short delay or test CTS/callback seam).  
- Host `NextQuestion` cancels pending advance (no double-advance).  
- Disabled: no schedule after Leaderboard.  
- Existing Host-Next tests remain green.

### README

- One sentence under Host flow: optional Auto-Advance after board; default off.

---

## Success criteria

- Default room behavior unchanged (manual Host Next).  
- With enable + 3s delay, a two-question quiz advances without Host click after each board.  
- Host click during countdown advances immediately once and does not fire a second advance.  
- CI (`dotnet test`) green.
