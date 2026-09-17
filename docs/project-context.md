# Projekt-Kontext — TrainArena

**Stand:** 2026-09-15  
**Lean:** positiv mit Cuts (`go-with-cuts`)  
**Arbeitstitel / Produktname:** **TrainArena** — **gelockt** (Entscheidung Thomas 2026-09-15)  
**Slug:** `trainarena`  
**Namenshistorie:** [quizduel-namen.md](./quizduel-namen.md)  
**Umzug:** [trainarena-umzug.md](./trainarena-umzug.md) · Handoff: [trainarena-handoff.md](./trainarena-handoff.md)  
**Motivation:** **kein Business** — persönlich nützlich + Spaß am Basteln (Ausbilder/Azubi Live-Quiz)

### Status (verbindlich)

- **Produktname gelockt:** TrainArena / `trainarena`.
- **Scaffold pausiert.** Kein Code-Skeleton in diesem Cursor-Project („Idee“).
- **Cursor Project transfer pending:** von „Idee“ → neues Project **TrainArena**.
- **Danach:** MVP-Skeleton erst im neuen Project.
- Checkliste: [trainarena-umzug.md](./trainarena-umzug.md).

---

## Entscheidung (verbindlich)

| Feld | Inhalt |
|---|---|
| **Produktname** | **TrainArena** (gelockt) · Slug `trainarena` |
| **Richtung** | Self-hosted Live-Quiz für **Ausbilder ↔ Azubis** (Kahoot-Alternative, LAN-first) |
| **Verdict** | Bauen — dünnes MVP; **Bau erst nach Project-Transfer** |
| **Stack** | **ASP.NET Core + SignalR** (C#) — **gelockt**; Player = leichtes HTML/JS. Vergleich: [quizduel-stack.md](./quizduel-stack.md) |
| **Deploy** | Docker und/oder self-contained Binary; MVP darf mit lokalem Dev-Server / Compose starten |
| **Verteilung** | **nur Self-host** (LAN / Download) — **gelockt**. Keine öffentliche Demo-Instanz vorerst. Kein App-Store. PWA später optional. Detail: [quizduel-verteilung.md](./quizduel-verteilung.md) |
| **Lizenz / Sichtbarkeit** | Public GitHub, **MIT** ok |
| **Umgebung** | Privatlaptop ok; **kein** Work-M365 / Firmen-VPN nötig |
| **Differenzierung (gewollt)** | Looks/UX vs ClassQuiz; **PowerUps** als Live-Quiz-Energie — **gewünscht, aber post-MVP (v1.1)** |
| **Nicht-MVP** | PowerUps (→ v1.1), öffentliche Demo, Store-Apps, Marketplace, Aula-Scale, SSO |

---

## Constraints (verbindlich fürs Weiterarbeiten)

1. Privat nutzbar und entwickelbar (kein Firmen-Account-Zwang).
2. Public OSS (MIT) angestrebt.
3. **Kein Business-Ziel** — Nutzen + Fun-to-build steuern Scope und Stack.
4. Use-Case bleibt **Ausbilder/Berufsschule/Ausbildung**, nicht generisches „Edu-Startup“.
5. Differenzierung: **UX/Polish + später PowerUps + Zero-Setup + Offline-LAN** — nicht Feature-Parität mit Kahoot.
6. **Self-host only** bis Thomas ausdrücklich Demo/Hosting öffnet. (**gelockt**)
7. Stack ist **ASP.NET Core + SignalR**; Player nicht als Blazor-Server-Circuit pro Handy. (**gelockt**)
8. **Kein Scaffold** unter Cursor Project „Idee“ — Transfer zuerst.

---

## MVP-Vertrag (kurz)

- Quiz-Editor (MC-Fragen)
- Host-Raum + QR/Code-Join
- Live-Runde, server-Timer, Leaderboard
- Ein Prozess, SQLite (o.ä.), Classroom-Größe (~≤40)

**Danach (v1.1):** PowerUps.

Alles andere: Backlog.

---

## Offene Fragen an Thomas

1. ~~Produktname~~ → **TrainArena** (geschlossen).
2. **Neues Cursor Project** anlegen (Name: **TrainArena**) — nächster Schritt. Siehe [trainarena-umzug.md](./trainarena-umzug.md).

Stack und Self-host bleiben gelockt. **Nicht** als Nächstes: MVP-Skeleton hier.

Einschätzung: [quizduel-einschaetzung.md](./quizduel-einschaetzung.md) · Native später: [quizduel-native.md](./quizduel-native.md)
