# TrainArena MVP — Brainstorm (Klärfragen)

**Stand:** 2026-09-17  
**Skill:** brainstorming (Fragen vor Design-Lock)  
**Sprache:** DE + EN wie in den Context-Docs.

---

## Status dieses Docs

| | |
|---|---|
| **Phase** | **BLOCKS beantwortet** — Design-/Plan-Lock möglich |
| **BLOCKS** | Q1=A · Q2=A · Q5=A zuerst, C+D nach Skeleton · Q7=A · Q8=A (B später) · Q11=A |
| **Rest** | Q3, Q4, Q6, Q9, Q10, Q12–Q20 = **Defaults übernommen** (A, außer Q17=C) |
| **Nächster Schritt** | Plan ist ready → Scaffold wenn Repo da (anderer Worker) |
| **Nicht jetzt** | App-Code in dieser Doc-Session |

Verknüpft: Plan [mvp-plan.md](./mvp-plan.md) · Context [project-context.md](./project-context.md) · Handoff [trainarena-handoff.md](./trainarena-handoff.md)

---

## Antwort-Tabelle (gelockt / Default)

| Q | Status | Entscheidung |
|---|---|---|
| **Q1** | **LOCKED** | **A** — Razor Pages / Minimal + JS (Host/Editor); Player HTML/JS |
| **Q2** | **LOCKED** | **A** — Laptop = Server+Host+Editor; Handys = Player |
| **Q3** | Default | **A** — Code + QR (Skeleton darf Code-first) |
| **Q4** | Default | **A** — 6 Zeichen uppercase |
| **Q5** | **LOCKED (phased)** | **Skeleton/erste Editor-Slice = A** (Text-MC, 4 Optionen, 1 richtig, Zeit optional). **C (Bilder) + D (Import CSV/Kahoot) = nach Skeleton, vor PowerUps** — blockieren Hub+Join+Demo-Frage **nicht** |
| **Q6** | Default | **A** — `/editor` separat; Host wählt Quiz |
| **Q7** | **LOCKED** | **A** — Basis + Speed-Bonus (Server-Empfangszeit, Cap) |
| **Q8** | **LOCKED (phased)** | **A** — Server-Timer → Reveal + Board → **Host Next**. **B (Auto-Advance) = later enhancement**, nicht Skeleton/MVP-Kern |
| **Q9** | Default | **A** — 20s Default, pro Frage überschreibbar |
| **Q10** | Default | **A** — Board nach jeder Frage + Finale |
| **Q11** | **LOCKED** | **A** — `dotnet run` first; Docker optional |
| **Q12** | Default | **A** — Quizzes SQLite; Live Memory |
| **Q13** | Default | **A** — Nickname unique; Host startet bei ≥1 |
| **Q14** | Default | **A** — SignalR reconnect + Connection-Count |
| **Q15** | Default | **A** — Deutsch first |
| **Q16** | Default | **A** — Seed-Quiz „Ausbildung Basics“ |
| **Q17** | Default | **C** — Ein aktiver Raum UI; `RoomId` im Modell |
| **Q18** | Default | **A** — Host spielt nicht mit |
| **Q19** | Default* | **A** Cuts, **außer** Bilder+Import: die sind **nach Skeleton / vor PowerUps** (siehe Q5) — nicht mehr „Non-MVP forever“ |
| **Q20** | Default | **A** — spielbare Runde + Editor-CRUD + Join/QR + Board + `dotnet run` README |

\* Q19-Liste in den Original-Docs nannte Bild-Fragen/Import als Non-MVP; durch Q5 überschrieben für die Phase „nach Skeleton, vor PowerUps“.

---

## Bereits gelockt (Produkt — nicht nochmal fragen)

| Thema | Entscheidung |
|---|---|
| Name / Slug | **TrainArena** / `trainarena` |
| Stack | **ASP.NET Core + SignalR** (C#); Player = leichtes HTML/JS |
| Verteilung | **Self-host only** — keine öffentliche Demo, kein App-Store-MVP |
| Lizenz | Public GitHub, **MIT** |
| PowerUps | **v1.1**, nicht MVP |
| Lean | Go with cuts — dünnes MVP zuerst |
| Umgebung | Privatlaptop; kein Work-M365 / Firmen-VPN |

---

## MVP-Vertrag (Anker)

- Quiz-Editor (MC) — zuerst Text-4er; später Bilder + Import
- Host-Raum + QR/Code-Join
- Live-Runde + Server-Timer + Leaderboard
- Ein Prozess + SQLite · Classroom ~≤40

**Skeleton-Slice zuerst:** Hub + Join + **eine** Demo-Frage end-to-end (**ohne** Bilder/Import).

---

## Ansatz — gelockt

| | Ansatz | Status |
|---|---|---|
| **A** | Ein ASP.NET-Prozess: **Razor/Minimal Host+Editor** + Static HTML/JS Player + SignalR Hub + SQLite | **gewählt (Q1=A, Q11=A)** |
| B | Blazor Server Host/Editor | verworfen |
| C | Separates Vite-Frontend | verworfen für MVP |

---

## Fragen (Referenz + Antworten)

### Q1 — Host-/Editor-UI · **BLOCKS** → **LOCKED A**

**Antwort Thomas (2026-09-17):** **A** — Razor Pages / Minimal API + JS.

- **A)** Razor Pages / Minimal API + etwas JS ✓
- B) Blazor Server Host/Editor
- C) Vanilla Static Host
- D) Später entscheiden

---

### Q2 — Happy-Path Rollen · **BLOCKS** → **LOCKED A**

**Antwort Thomas (2026-09-17):** **A** — Laptop = Server + Host-UI + Editor; Azubi-Handys = nur Player.

---

### Q3 — Join-Flow · Default **A**

Code **oder** QR; Skeleton darf mit **nur Code** starten, QR danach.

---

### Q4 — Room-Code · Default **A**

6 Zeichen, alphanumerisch uppercase.

---

### Q5 — Quiz-Authoring · **BLOCKS** → **LOCKED (phased)**

**Antwort Thomas (2026-09-17):** will **C+D** (Bilder + Import), aber:

| Slice | Scope |
|---|---|
| **Skeleton + erste Editor-Slice** | **A** — Titel, Text-MC, genau 4 Antworten, 1 richtig, optionales Zeitlimit |
| **Nach Skeleton, vor PowerUps** | **C** Bilder in Fragen · **D** Import CSV/Kahoot |
| **Blockiert Scaffold?** | **Nein** — Hub+Join+Demo-Frage bleibt Text-only |

Optionen (Referenz):

- **A)** Text-MC 4/1 + Zeit
- B) 2–6 Antworten
- **C)** A + Bilder
- **D)** A + Import

---

### Q6 — Editor-Ort · Default **A**

Eigene `/editor`-Seite; Host wählt fertiges Quiz.

---

### Q7 — Scoring · **BLOCKS** → **LOCKED A**

**Antwort Thomas (2026-09-17):** **A** — Basispunkte bei richtig + Speed-Bonus aus Server-Empfangszeit (Cap); falsch = 0.

---

### Q8 — Timer & Rundenfluss · **BLOCKS** → **LOCKED A** (+ B later)

**Antwort Thomas (2026-09-17):**

| Jetzt (MVP-Kern / Skeleton) | Später |
|---|---|
| **A** Server start/end → Reveal + Leaderboard → **Host Next** | **B** Auto-Advance als optionales Follow-up — **nicht** Skeleton |

- C (Pause/Verlängern) bleibt post-MVP / Backlog.

---

### Q9 — Zeitlimit · Default **A** — 20s, pro Frage überschreibbar

### Q10 — Leaderboard · Default **A** — nach jeder Frage + Finale

### Q11 — Run-Story · **BLOCKS** → **LOCKED A**

**Antwort Thomas (2026-09-17):** **A** — `dotnet run` lokal zuerst; Docker Compose optional dokumentieren.

---

### Q12–Q18, Q20 · Defaults

Wie in der Antwort-Tabelle (Q12=A … Q16=A, Q17=C, Q18=A, Q20=A).

### Q19 — Cuts · Default A mit Q5-Override

Weiterhin **raus:** PowerUps (→ v1.1) · öffentliche Demo · Store · Marketplace · Aula-Scale · SSO · Teams · Sound-Packs · PWA-Versprechen · Analytics · Kahoot-Parität · Multi-Tenant-Auth · Spectator · andere Frage-Typen.

**Nicht mehr „ewig raus“:** Bild-Fragen (C) und CSV/Kahoot-Import (D) — Phase **nach Skeleton, vor PowerUps**.

---

## Scaffold: blockiert vs. defaulten

### BLOCKS erledigt — Scaffold darf starten (sobald Repo da)

1. ~~Q1~~ A  
2. ~~Q2~~ A  
3. ~~Q5~~ phased (A jetzt; C+D später)  
4. ~~Q7+Q8~~ A / A (+ B later)  
5. ~~Q11~~ A  

### Defaults ohne weitere Rückfrage

| Frage | Default |
|---|---|
| Q3 | Code zuerst, QR danach |
| Q4 | 6 Zeichen uppercase |
| Q6 | `/editor` separat |
| Q9 | 20s |
| Q10 | Board nach jeder Frage |
| Q12 | Quiz SQLite, Live Memory |
| Q13–Q16, Q18, Q20 | A |
| Q17 | C |
| Q19 | Cuts + Q5-Override |

---

## Nächster Prozess-Schritt

1. ~~Thomas BLOCKS~~ → erledigt 2026-09-17.  
2. Ansatz A gelockt.  
3. [mvp-plan.md](./mvp-plan.md) = **answers locked / ready for implementation**.  
4. Scaffold wenn Repo bereit (anderer Worker) — **kein** App-Code in dieser Doc-Session.

---

*Kein App-Code in diesem Schritt.*
