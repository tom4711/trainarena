# TrainArena — Variable Question Types Design

**Status:** Approved for implementation planning (Thomas, 2026-10-09)  
**Branch:** `cursor/question-types-design-b12d`  
**Depends on:** MVP Editor + Live Round + PowerUps + Auto-Advance on `main`  
**Follow-up (out of this spec):** Frage-Review / per-question answer stats

## Goal

Support **Wahr/Falsch** and **Multiple Choice with 2–6 options** (still exactly one correct answer), without breaking existing 4-option quizzes, CSV imports, or Power-Ups. Live wire format already uses `string[] Options`; persistence and editor catch up.

## Locked decisions

| Topic | Choice |
|-------|--------|
| Order vs Review | **B** — Question types first; Frage-Review = separate later spec |
| Types in v1 | **B** — Wahr/Falsch + variable MC 2–6 options; still single correct |
| 50/50 with fewer options | **B** — Scales: ≥4 → mask 2 wrong; =3 → mask 1 wrong; ≤2 → unavailable |
| Wahr/Falsch modeling | **C** — Dedicated editor UI type; same MC storage model internally |
| CSV / Kahoot import | **A** — Backward compatible; optional Option E/F (Answer 5/6); ≥2 filled options OK |
| Data model | **1** — Fixed columns `Option0`…`Option5` + `DisplayKind` |

## Non-goals

- Multi-correct answers, free text, other question types  
- Frage-Review / answer aggregates after the round (follow-up spec)  
- Export CSV symmetry  
- Auto-detecting Wahr/Falsch from import text  
- Breaking change to existing 4-option TrainArena CSVs  
- More than 6 options  

---

## §1 Data model & validation

### `Question` entity changes

| Field | Rule |
|-------|------|
| `Option0`…`Option5` | `Option0` and `Option1` required (non-whitespace). `Option2`…`Option5` optional; empty string = absent |
| Contiguity | No gaps: if `OptionN` is set, `Option0`…`OptionN−1` must be set |
| `CorrectIndex` | Must reference an **present** option (`0 … optionCount−1`) |
| `DisplayKind` | `Mc` \| `TrueFalse` — editor/UX only; gameplay uses filled option count |
| Existing rows | Remain valid (4 options, treat missing `DisplayKind` as `Mc`) |

### Wahr/Falsch storage

Editor sets `DisplayKind = TrueFalse`, `Option0 = "Wahr"`, `Option1 = "Falsch"`, `Option2`…`Option5` empty; user picks which side is correct. Game session treats it as a 2-option MC.

### `QuizRules`

Replace hard-coded “all four required”:

- Text non-empty  
- Collect trailing-trimmed options; count must be 2–6 with no gaps  
- `CorrectIndex` in range  
- When `DisplayKind == TrueFalse`, enforce exactly two options with fixed labels “Wahr” / “Falsch”

### Mapping

`ToDemoQuestion` returns only **filled** options as `string[]` (length 2–6). `DemoQuestion` and `QuestionStartedMessage` already use `string[]` — no wire-shape change required beyond length variance.

### SQLite

Add nullable/empty-default columns `Option4`, `Option5`, and `DisplayKind` (default `Mc`). Existing DBs must remain playable after migrate/`EnsureCreated` path used by the app.

---

## §2 Live round, Hub, Power-Ups

| Area | Behavior |
|------|----------|
| Start question | Broadcast current `Options[]` length 2–6; Host/Player render dynamically |
| Submit answer | `optionIndex` must be `0 … Options.Length−1`; else reject |
| Reveal | `CorrectIndex` unchanged; UI highlights only existing buttons |
| 50/50 | `optionCount ≥ 4` → mask 2 wrong; `= 3` → mask 1 wrong; `≤ 2` → reject + UI disabled |
| Other power-ups | Double, Extra time, Shield, Disrupt, Host arena events — unchanged |
| Reconnect | Sync current `Options[]` + mask state as today |

### 50/50 implementation

Replace `PickTwoWrongIndexes` (hard-coded range 0–3) with:

`PickWrongIndexes(correctIndex, optionCount, maskCount)`

- `maskCount = optionCount >= 4 ? 2 : optionCount == 3 ? 1 : 0`  
- If `maskCount == 0`, use path returns error (do not invent a meaningless mask)  
- `MaskedWrongIndexes` length equals `maskCount` (clients already treat as int array)

`PlayerPowerUpState.MaskedWrongIndexes` comment (“length 2”) updates to “length 1 or 2”.

---

## §3 Editor & import

### Editor UX

- Type selector: **Multiple Choice** | **Wahr/Falsch**  
- **MC:** dynamic option fields 2–6 (add/remove); mark exactly one correct; default still 4 options  
- **Wahr/Falsch:** fixed Wahr/Falsch labels; radio for correct side; persist `DisplayKind=TrueFalse`  
- Question list: show only filled options; small badge when `TrueFalse`  
- Existing 4-option questions: editable as MC without migration pain  

### Import (backward compatible)

| Format | Change |
|--------|--------|
| TrainArena CSV | At least two of `OptionA`…`OptionD` filled; optional `OptionE` / `OptionF`; empty extras = fewer options |
| Kahoot-like | `Answer 1`…`Answer 6` analogously; rows with only 2–4 answers valid |
| Old 4-option CSVs | Remain valid unchanged |
| `DisplayKind` | Always `Mc` on import (no Wahr/Falsch detection) |

Invalid rows (gaps, correct out of range, &lt;2 options) skip + error line, same as today.

### README

Document optional E/F columns and that 2–6 answers are allowed; note Wahr/Falsch is editor-only shortcut.

---

## §4 Errors, tests, migration

### Errors

- Editor: German validation messages (gaps, &lt;2/&gt;6 options, correct out of range)  
- Hub: bad `optionIndex` → `AnswerAccepted { ok:false }`; 50/50 on ≤2 options → `PowerUpUsed { ok:false }` / `PowerUpError`  
- Import: skip row + error list  

### Tests (extend existing suites)

- `QuizRules` / persistence: 2-, 3-, 6-option OK; gaps rejected  
- Import: legacy 4-column CSV + new E/F + Kahoot with 2 answers  
- `GameSession`: answer index bounds; 50/50 maskCount 2 → 1 → reject  
- Hub / full round: Wahr/Falsch round end-to-end  
- Existing 4-option fixtures stay green except where “four required” was asserted  

### Success criteria

1. Create MC with 2, 3, or 6 options in editor → playable round  
2. Create Wahr/Falsch → two buttons, correct scoring  
3. Old seed / existing quizzes still work  
4. Legacy CSV import still works; new E/F columns work  
5. 50/50 behaves per option count rules above  

---

## §5 Follow-up: Frage-Review (not this spec)

Separate design after this ships. Expected dependency: retain per-question answer aggregates through Reveal (today `_answersThisQuestion` is cleared each question). Review UX and persistence are explicitly deferred so this slice stays focused.

---

## Components touched (implementation hint)

| Unit | Change |
|------|--------|
| `Question` entity + `AppDbContext` | `Option4`/`Option5`/`DisplayKind` |
| `QuizRules`, Editor pages | Validation + UX |
| `QuizImport` / `ImportedQuestion` | Variable options + E/F |
| `GameSession` 50/50 + answer bounds | Dynamic option count |
| Host/Player JS/CSS | Dynamic option buttons; disable 50/50 when ≤2 |
| README | Import + types docs |
| Tests listed in §4 | |

---

*No application code in the design-doc commit; implementation follows `writing-plans` after user reviews this file.*
