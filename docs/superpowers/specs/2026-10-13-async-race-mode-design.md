# Design: Async-Rennen (separater Gamemode)

**Datum:** 2026-10-13  
**Status:** Implemented (Approved)  
**Ziel:** Optionaler Raum-Modus, in dem jeder Spieler Fragen in eigener Reihenfolge und in eigenem Tempo spielt; Host sieht Live-Punkte und Fortschritt `x/n`. Sync-Klassikmodus bleibt unverändert.

## Entscheidungen (Brainstorming)

| Thema | Wahl |
|-------|------|
| Produktform | Separater Modus, kein Umbau des Sync-Kerns |
| Ambition | Power-Ups inkl. Störimpuls |
| Host-Sicht | Punkte + Fortschritt `x/n` (kein Fragetext) |
| Rennende | Automatisch, wenn alle Spieler alle Fragen fertig haben |
| Architektur | Parallel-Runner in `GameSession` (`PlayerRaceState`) |

## Scope

**In**

- CreateRoom-Flag `asyncRace: { enabled: bool }` (UI-Label z. B. „Async-Rennen“)
- Pro Spieler: Shuffle der Quiz-Fragen, eigener Index/Timer/Antwortzyklus
- Power-Ups: 50/50, Double, Extra-Zeit, Shield am eigenen RaceState; Störimpuls auf Zielspielers *aktueller* offener Frage
- Host-Scoreboard: Nickname, Score, `answeredCount`/`total`
- Auto-Finish → Abschluss-Rangliste
- Hub-/Session-Tests für Async; Sync-Regression

**Out (MVP)**

- Umbau oder Entfernen des Sync-Pfads
- Host sieht Fragetext oder gemeinsame Reveal-Show
- Kombination Async + Prüfungsmodus (Exam) und Async + Auto-Advance: MVP **nicht kombinierbar** (UI/Server ablehnen oder Controls disablen)
- Fester Shuffle-Seed / Replay (optional später)

## CreateRoom & Regeln

- Wire: `asyncRace: { enabled }` neben bestehenden CreateRoom-Optionen.
- Wenn `asyncRace.enabled`:
  - Auto-Advance aus (ignorieren/disablen)
  - Exam aus / nicht kombinierbar
- Klassikmodus: bestehendes Verhalten 1:1.

## Session-Modell

- `GameSession.AsyncRaceEnabled` (bool, bei Create gesetzt).
- **`PlayerRaceState`** (pro Spieler, keyed wie Player):
  - `Order`: `IReadOnlyList<DemoQuestion>` (Shuffle-Kopie)
  - `Index`: aktuelle Frage (0-based), oder „fertig“
  - `EndsAt`: Deadline der aktuellen Frage
  - `Finished`: bool
- Inventar/Score bleiben am bestehenden Player-Record; Race-Fortschritt am `PlayerRaceState`.
- Raum-Phasen im Async-Modus: `Lobby` → `AsyncRacing` (nach Host-Start) → `Finished`.
- Kein gemeinsames room-wide `QuestionActive` / Zwischen-`Leaderboard` zwischen Fragen.
- Sync-API (`StartQuestion` global, room `ShowLeaderboard` zwischen Fragen) nur wenn `!AsyncRaceEnabled`.
- Async-API (Namen illustrativ): `StartAsyncRace`, Submit-Pfad-Branch, Timeout-Tick pro Spieler, `GetAsyncScoreboard()`.
- Shuffle: Fisher-Yates pro Spieler beim Start (`Random`); später optional Seed.

## Hub / Protokoll

**Start:** Host startet Rennen → Server baut RaceStates, schickt jedem Spieler seine erste Frage (QuestionStarted-ähnliches Payload, **targeted**), Broadcast `AsyncScoreboard` an Host (und ggf. Gruppe).

**Antwort:** `SubmitAnswer` im Async-Branch:
1. Werten gegen aktuelle Frage des Spielers (Speed/Shield/Double wie Sync-Regeln, soweit übertragbar)
2. Kurzes persönliches Feedback (richtig/falsch, optional Explanation)
3. Nächste Frage an denselben Spieler **oder** `Finished` für ihn
4. `AsyncScoreboard` aktualisieren

**Timer:** Server-Timeout pro `PlayerRaceState` (analog ForceEnd, aber lokal): unbeantwortete Frage → 0 / Regeln wie Sync-Timeout, dann Advance.

**Reconnect:** Caller auf seine aktuelle Frage + Inventar + Scoreboard-Snapshot syncen.

**Scoreboard DTO (Skizze):**  
`{ entries: [ { nickname, score, answeredCount, total } ] }` — Sortierung nach Score (dann Nickname).

## Power-Ups

| Power-Up | Async-Verhalten |
|----------|-----------------|
| 50/50 | Maskiert Optionen der *eigenen* aktuellen Frage |
| Double | Nächste eigene Wertung |
| Extra-Zeit | Verlängert eigenes `EndsAt` |
| Shield | Wie Sync, eigener Spieler |
| Störimpuls | Ziel = anderer Spieler mit **offener** aktueller Frage; fertige Spieler nicht wählbar. Effekt auf Ziel-UI/State (bestehende Disrupt-Semantik, Zielmenge = „fragt gerade“) |

Host-Power-Ups: MVP unverändert aus oder nur wenn sie ohne gemeinsamen Fragen-Takt Sinn ergeben — **MVP: Host-Disrupt/Skip-Fragen nicht Teil des Async-Rennens** (nur Spieler-Power-Ups), außer bereits raumabhängige Starter-Inventare beim Join.

## UI

**Host**

- Checkbox „Async-Rennen“ bei Raumerstellung; disablen/widersprechen mit Exam & Auto-Advance.
- Während Race: Liste Nickname · Punkte · `x/n`; kein „Nächste Frage“, kein Fragetext.
- Ein Start-Button („Rennen starten“); Ende automatisch → Abschluss-Rangliste.

**Spieler**

- Bestehender Frage-Screen (Meta/Timer/Power-Up-Chips); nach Antwort Feedback, dann nächste Frage ohne Host-Wartepanel.
- Störimpuls-Zielpicker: nur Spieler mit offener Frage.

## Fehlerfälle

- Submit ohne aktive eigene Frage → Fehler / ignore.
- Disrupt auf fertigen / sich selbst / ungültiges Ziel → `PowerUpError`.
- CreateRoom mit Async+Exam → Reject mit klarer Meldung.
- Leeres Quiz → Start verweigern (wie Sync).

## Abnahme

- Zwei Spieler, unterschiedliche Fragenreihenfolge beobachtbar (Logs oder gezielter Test).
- Unabhängige Timer/Fortschritte; Host `x/n` stimmt.
- Disrupt trifft nur aktive Frager.
- Alle fertig → Finished + Rangliste.
- Sync-Raum unverändert (Smoke/bestehende Tests grün).

## Nicht-Ziele

- Direkter Umbau, bei dem Sync verschwindet  
- Client-only Shuffle bei weiter sync Server  
- Volle Async+Exam-Parität im ersten Slice  
