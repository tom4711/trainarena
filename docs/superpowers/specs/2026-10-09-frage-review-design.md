# TrainArena — Frage-Review Design

**Status:** Approved for implementation planning (Thomas, 2026-10-09)  
**Branch:** `cursor/frage-review-design-b12d`  
**Depends on:** Variable question types + live round + PowerUps on `main`  
**Follow-ups (out of this spec):** Spieler-Eigenreview · SQLite-History · CSV-Export · Nicknames pro Option · Sortierung nach Schwierigkeit · Review nach jeder Frage

## Goal

After a finished round, the **Host** sees a **Fragen-Review** under the final leaderboard: per question, percent correct and answer distribution — for classroom debrief. Data lives only in the live session (memory).

## Locked decisions

| Topic | Choice |
|-------|--------|
| Audience | **A** — Host only (aggregates for teaching debrief) |
| Content | **B** — % correct + answer distribution (no nicknames per option) |
| When / where | **A** — Only at Finale (`GameFinished`), under the ranking on the Host page |
| Persistence | **A** — Live memory only (gone with new room / process restart) |
| Architecture | **1** — Session aggregates + extend `GameFinished` payload |

## Non-goals

- Player self-review / showing review on player clients  
- SQLite persistence or multi-round history  
- CSV/PDF export  
- Nicknames per option  
- Sort by worst % first  
- Per-question review on the mid-round leaderboard  

---

## §1 Session data model

Retain a snapshot when each question transitions to **Reveal**, **before** clearing `_answersThisQuestion`.

### `QuestionReviewStat` (on `GameSession`)

| Field | Meaning |
|-------|---------|
| `QuestionIndex` | 0-based index in the round |
| `Text` | Question text |
| `Options` | Filled options (`string[]`, length 2–6) |
| `CorrectIndex` | Correct option index |
| `Counts` | `int[]` same length as `Options` — how often each option was chosen |
| `AnsweredCount` | Players who submitted an answer |
| `PlayerCount` | Connected players at Reveal snapshot (denominator for „X von Y“) |
| `CorrectCount` | Submissions where chosen index == `CorrectIndex` |

**CorrectCount rule:** content-correct (selected option equals `CorrectIndex`), **not** points-based. A disrupted player who still picks the right option increments `CorrectCount`; zero points do not change that. Distribution uses actual selected indexes.

Append-only list on `GameSession` (e.g. `IReadOnlyList<QuestionReviewStat> Review`). Cleared implicitly when a new room/session is created.

Players who never answer: not in `Counts`; they reduce neither `AnsweredCount` nor option buckets. Unanswered questions still appear in the review with zero counts.

---

## §2 Hub, wire, Host UI

### Wire

Extend:

```csharp
GameFinishedMessage(
    IReadOnlyList<LeaderboardEntryDto> Entries,
    IReadOnlyList<QuestionReviewDto> Review);

QuestionReviewDto(
    int Index,
    string Text,
    string[] Options,
    int CorrectIndex,
    int[] Counts,
    int AnsweredCount,
    int PlayerCount,
    int CorrectCount);
```

Emit on the existing `GameFinished` broadcast. Player clients ignore `Review`.

**Reconnect:** When syncing a client already in `GamePhase.Finished`, send the same `GameFinished` including `Review` so the Host can rebuild the UI.

### Host UI

- Below the final leaderboard: section „Fragen-Review“  
- Quiz order (not sorted by difficulty)  
- Per question: number + text; **% richtig** = `CorrectCount / AnsweredCount` (show „—“ / n/a if `AnsweredCount == 0`); „X von Y geantwortet“ with `X = AnsweredCount`, `Y = PlayerCount` (connected players at Reveal)  
- Option rows with counts; highlight correct option (reuse reveal styling)  
- Scrollable if many questions; Beamer-friendly (readable type, no dense cards overload)

**Player:** unchanged finale (leaderboard / thank-you); do not render review.

### Capture timing

In `TransitionQuestionToReveal` (or equivalent single place before clearing answer maps): build `Counts` from `_answersThisQuestion`, compute `AnsweredCount` / `CorrectCount`, copy current question text/options/correct index, append to `Review`.

---

## §3 Edge cases & tests

| Case | Behavior |
|------|----------|
| Zero answers | All counts 0; % = n/a; question still listed |
| One player | Normal aggregates |
| Power-ups | Distribution = chosen indexes; CorrectCount = option match |
| TrueFalse / 2–6 options | `Counts.Length == Options.Length` |
| Single-question round | One review entry at finale |
| Host reconnect after finish | Review restored via Finished sync |

### Tests

- Unit: after Reveal, stat appended; counts/correct match (wrong + no-answer)  
- Disruption: right option selected → `CorrectCount` +1 even if 0 points  
- Hub / full round: `GameFinished.Review.Count ==` question count; DTO fields populated  
- Update existing Finished/leaderboard tests for the new `Review` argument  

### Success criteria

1. Finish a multi-question round → Host sees review under ranking with % and bars/counts.  
2. Host SignalR reconnect in Finished → review still visible.  
3. Player UI unchanged.  
4. No SQLite schema change.

---

## Components touched (implementation hint)

| Unit | Change |
|------|--------|
| `GameSession` | `Review` list + snapshot on Reveal |
| `HubMessages` / `GameHub` | Extend `GameFinished`; Finished sync |
| `wwwroot/js/host.js` (+ CSS if needed) | Render review under finale |
| Tests | Session + hub/full-round |

---

*No application code in the design-doc commit; implementation follows `writing-plans` after user reviews this file.*
