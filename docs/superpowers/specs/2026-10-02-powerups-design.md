# TrainArena v1.1 — PowerUps Design

**Status:** Approved for implementation planning (Thomas, 2026-10-02)  
**Branch:** `cursor/powerups-v11-47f9`  
**Depends on:** MVP Phases 0–5 (live round, Host Next, editor, Docker)

## Goal

Add **Live-Quiz-Energie** without breaking the Ausbildung classroom tone: personal player jokers plus optional host arena events, lightly competitive, server-authoritative.

## Locked decisions

| Topic | Choice |
|-------|--------|
| Who uses them | **C** — Players (personal) + Host (arena events) |
| Economy | **A+B mix** — starter inventory + streak rewards, **per-room configurable** |
| Competition | **B** — lightly competitive (no hard PvP: no freeze / steal / swap) |
| Catalog size | **A** — 4 player + 2 host types for v1.1 |
| Activation window | **A** — only during `QuestionActive` |
| Architecture | **1** — effects in `GameSession` (+ small helpers), fixed catalog in code |

## Non-goals (v1.1)

- Persistent power-up stats / leaderboards of usage  
- Shop, currency grind, sound packs  
- Editor-defined custom power-ups  
- Hard PvP (freeze, steal points, answer swap)  
- Auto-Advance (separate later enhancement)  
- Changing core scoring formula beyond multipliers from power-ups  

---

## §1 Catalog & effects

### Player power-ups (consume 1 from inventory)

Usable only in `GamePhase.QuestionActive`, and only **before** the player has submitted an answer for the current question (except where noted).

| ID | Name (DE UI) | Effect |
|----|--------------|--------|
| `fifty_fifty` | 50/50 | Server picks **2 wrong** option indexes; only this player receives masked options. Does not change the correct index. |
| `double` | Double | Next **correct** answer this question: award `points * 2` (base + speed bonus). Spent even if answer wrong / timeout (no refund). |
| `extra_time` | Extra-Zeit | If this question was not already extended by a player `extra_time`, set `QuestionEndsAtUtc += 5s`, broadcast new end, reschedule ForceEnd. Does **not** stack if several players use it. Same timer model as host `time_plus`. |
| `shield` | Shield | Consumes on use; sets `HasShield` until it absorbs one effect marked `IsCompetitiveTargeting`, then clears. May run in parallel with one other self-effect. |

**Lightly competitive (v1.1):** No offensive/targeting player power-up in the minimal set. Pressure comes from asymmetric personal advantages (50/50, double, extra_time). Host events are global buffs (`IsCompetitiveTargeting = false`). **Shield ships in catalog + streak pool** so the economy stays interesting; it is inert until a targeting effect exists (v1.1.x). No fake targeting effect is added only to justify Shield.

### Host arena events

Usable only in `QuestionActive`, host connection only.

| ID | Name (DE UI) | Effect |
|----|--------------|--------|
| `boost_all` | Team-Boost | All players: next correct answer this question scores `floor(points * 1.5)`. |
| `time_plus` | Zeit +5 | `QuestionEndsAtUtc += 5s`; broadcast updated end time to group; reschedule server end timer. |

### Concurrent rules

- Max **one** active self-effect among `{fifty_fifty, double, extra_time}` at a time.  
- `shield` may be active in parallel.  
- Host: default **1 arena event total per question** (`MaxHostEventPerQuestion`).  
- All grants/consumes/effects are **server-authoritative**.

---

## §2 Economy & room config

### `PowerUpRoomConfig` (set at `CreateRoom`)

| Field | Default | Notes |
|-------|---------|-------|
| `Enabled` | `true` | Master switch; if false, no inventory UI / hub methods reject |
| `Starter` | `{ fifty_fifty: 1, double: 1, extra_time: 0, shield: 1 }` | Copied to each player on join |
| `StreakRewardEvery` | `2` | `0` = streak rewards off |
| `StreakRewardPool` | all four player IDs | On reward: pick 1 at random (uniform) |
| `MaxStackPerType` | `3` | Cap when granting |
| `HostEventsEnabled` | `true` | |
| `MaxHostEventPerQuestion` | `1` | Total host arena uses per question |

Host lobby UI: compact toggles/numbers (enabled, streak every, starter counts)—not a large settings surface.

### Grant / consume flow

1. **Join** → copy `Starter` into `PlayerPowerUpState.Inventory`.  
2. **UsePowerUp** → if valid, decrement inventory, apply effect, notify caller (+ group if needed).  
3. **Question ended** → for each player: if answered correctly this question → `Streak++`, else `Streak = 0`. If `StreakRewardEvery > 0` and `Streak > 0` and `Streak % StreakRewardEvery == 0` → grant 1 from pool (respect cap) → `InventoryUpdate`.  
4. **Reconnect** → send current inventory + active effect flags.

---

## §3 Architecture & hub

### Code units

| Unit | Responsibility |
|------|----------------|
| `PowerUpId` | Stable string/enum IDs |
| `PowerUpCatalog` | Static metadata (name, kind: Player/Host) |
| `PowerUpRoomConfig` | Room options + defaults/validation |
| `PlayerPowerUpState` | Inventory, streak, active flags, personal deadline |
| `PowerUpEffects` / methods on `GameSession` | Apply/validate use; scoring hooks |
| `GameHub` | `UsePowerUp`, `HostArenaEvent`; extend `CreateRoom` |

Keep logic in session layer so unit tests do not need SignalR.

### Scoring hook

Existing server scoring path (base + speed) remains. After computing `points` for a correct answer:

1. If player has active `double` for this question → `points *= 2`, clear double.  
2. If room has active `boost_all` for this question → `points = floor(points * 1.5)`.  
3. Order: **double first, then boost_all** (explicit). Wrong/timeout: clear pending double without points.

### Hub contract additions

| Direction | Name | Payload (sketch) |
|-----------|------|------------------|
| C→S | `CreateRoom` | `{ quizId, powerUpConfig? }` — omit → defaults |
| C→S | `UsePowerUp` | `{ powerUpId }` |
| C→S | `HostArenaEvent` | `{ powerUpId }` |
| S→C | `InventoryUpdate` | `{ counts: {…}, streak }` |
| S→C | `PowerUpUsed` | `{ powerUpId, ok, detail? }` (caller; e.g. masked indexes for 50/50) |
| S→C | `ArenaEvent` | `{ powerUpId, endsAtUtc? }` (group) |
| S→C | `PowerUpError` | `{ error }` (caller) |
| S→C | `QuestionStarted` | unchanged + clients already have inventory from lobby/join |

### Rejection reasons (caller-only `PowerUpError`)

- Power-ups disabled  
- Wrong phase  
- Not host / not in room  
- Unknown id / wrong kind for caller  
- Empty inventory  
- Already answered this question  
- Self-effect already active  
- Host event budget exhausted this question  

### UI (minimal)

- **Host lobby:** config strip when creating room.  
- **Host live:** two arena buttons when `QuestionActive` and budget left.  
- **Player:** inventory row during question; disabled after submit; show personal timer if `extra_time`.  
- German labels as in §1.

### Testing

- Unit: config validation; starter on join; streak grant/cap; fifty_fifty masks two wrong; double/boost scoring order; extra_time accept window; host event budget; illegal phase.  
- Hub integration: `UsePowerUp` + `HostArenaEvent` happy path + one rejection.  
- Manual: 2 browsers, enable/disable, streak reward visible.

### Rollout

- Feature lives behind `PowerUpRoomConfig.Enabled` (default on).  
- No DB migration required (in-memory session state only).  
- README: short v1.1 PowerUps section after implementation.

---

## Resolved during design

| Point | Resolution |
|-------|------------|
| Shield with no targeting effect in v1.1 | In catalog/pool; inert until `IsCompetitiveTargeting` effect exists |
| Multiplier order | Double, then Team-Boost (`floor`) |
| Extra-Zeit vs global timer | First player `extra_time` per question extends global `EndsAtUtc` by 5s once (non-stacking); same ForceEnd path as `time_plus` |
| Stacking `extra_time` + `time_plus` | Both may extend the same question (each applies its own +5 when used); document in tests |

---

## Success criteria

- Host can disable power-ups per room.  
- With defaults, a 2-player demo shows inventory, 50/50 mask, double score, streak grant, and one host arena event.  
- Existing MVP tests remain green; new power-up tests cover §3 Testing list.  
- No Auto-Advance introduced.
