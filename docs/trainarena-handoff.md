# TrainArena — Handoff (neues Project)

**Stand:** 2026-09-15 · Paste in den neuen Cursor-Project-Chat.

---

## Was es ist

**TrainArena** — self-hosted Live-Quiz für **Ausbilder ↔ Azubis** (Kahoot-Alternative, LAN-first). Persönlich nützlich + Fun-to-build; **kein Business-Ziel**.

## Entscheidungen (gelockt)

| | |
|---|---|
| **Name / Slug** | TrainArena / `trainarena` |
| **Stack** | ASP.NET Core + SignalR (C#); Player = leichtes HTML/JS (kein Blazor-Circuit pro Handy) |
| **Verteilung** | **Self-host only** (LAN / Download). Keine öffentliche Demo, kein App-Store-MVP |
| **Lizenz** | Public GitHub, MIT ok |
| **PowerUps** | **v1.1** — nicht MVP |
| **Umgebung** | Privatlaptop; kein Work-M365 / Firmen-VPN |

## MVP-Vertrag

- Quiz-Editor (MC)
- Host-Raum + QR/Code-Join
- Live-Runde, Server-Timer, Leaderboard
- Ein Prozess, SQLite (o.ä.), Classroom ~≤40

## Non-Goals (MVP)

PowerUps · öffentliche Demo · Store-Apps · Marketplace · Aula-Scale · SSO · Feature-Parität mit Kahoot

## Constraints

Privat entwickelbar · OSS MIT · Use-Case Ausbildung (nicht generisches Edu-Startup) · Differenzierung über UX/Polish + später PowerUps + Zero-Setup + Offline-LAN

## Herkunft

Ideation unter Cursor Project **„Idee“**. Context-Docs: siehe [trainarena-umzug.md](./trainarena-umzug.md). Scaffold dort **abgebrochen** — Bau nur hier.

## Kickoff-Satz

`Context übernommen, MVP starten`
