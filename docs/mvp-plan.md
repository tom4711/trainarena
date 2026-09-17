# TrainArena MVP — Implementation Plan

> **Status: answers locked / plan ready for implementation** (2026-09-17)  
> Quelle: [mvp-brainstorm.md](./mvp-brainstorm.md). **Kein App-Code in dieser Doc-Session.** Repo = anderer Worker.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Self-hosted Live-Quiz (Ausbilder-Laptop → Azubi-Handys): Editor (MC) · Host-Raum + QR/Code-Join · Live-Runde mit Server-Timer · Leaderboard — ein ASP.NET-Prozess + SQLite, Classroom ≤40.

**Architecture:** Ein long-lived ASP.NET Core Prozess: **Razor Pages / Minimal + JS** für Host+Editor (Q1=A), Static HTML/JS Player, SignalR `GameHub` (Rooms/Groups, server-authoritative Timer & Scoring). Quizzes in SQLite; aktive Session vor allem im Memory. Player **ohne** Blazor-Server-Circuit.

**Tech Stack:** .NET 8 · ASP.NET Core · SignalR · SQLite (EF Core oder Dapper) · **Razor Pages / Minimal + JS** Host/Editor · Vanilla HTML/JS + `@microsoft/signalr` Player · `dotnet run` first (Docker optional später).

## Global Constraints

- Produktname **TrainArena** / Slug `trainarena` — gelockt.
- Stack **ASP.NET Core + SignalR (C#)**; Player leichtes HTML/JS — gelockt.
- Host/Editor-UI = **Razor/Minimal + JS** (Q1=A) — gelockt.
- Happy-Path: **Laptop = Server+Host+Editor; Handys = Player** (Q2=A) — gelockt.
- **Self-host only** (LAN/Download); keine öffentliche Demo; kein App-Store-MVP — gelockt.
- Lizenz **MIT**; Public GitHub — gelockt.
- **PowerUps = v1.1**, nicht MVP — gelockt.
- Scoring = Basis + Speed-Bonus (Q7=A); Timer-Fluss = Host Next nach Reveal/Board (Q8=A).
- Classroom-Ziel **~≤40**; kein Aula-Scale, kein SSO, kein Marketplace.
- Server-authoritative Timer & Scoring; Client-Clock nie Quelle der Wahrheit.
- Privatlaptop-Dev; kein Work-M365 / Firmen-VPN nötig.

---

## Lock-Status & Entscheidungen

| | |
|---|---|
| **Plan-Stand** | **Ready for implementation** — BLOCKS gelockt, Defaults übernommen |
| **Skeleton zuerst** | Hub + Join + **eine** Text-Demo-Frage E2E (**ohne** Bilder/Import) |
| **Repo** | Anderer Worker — Zielstruktur unten annehmen |

### Gelockte / Default-Entscheidungen

| ID | Entscheidung | Quelle |
|---|---|---|
| A1 | Host/Editor = Razor Pages / Minimal + JS | **Q1=A LOCKED** |
| A2 | Laptop = Server+Host+Editor; Handys = Player | **Q2=A LOCKED** |
| A3 | Join: Code + QR (QR nach Code-Skeleton) | Q3=A Default |
| A4 | Room-Code: 6 Zeichen uppercase | Q4=A Default |
| A5a | **Skeleton + Editor-Core:** Titel, MC 4/1, Zeit optional — **Text only** | **Q5 Phase 1 = A** |
| A5b | **Nach Skeleton, vor PowerUps:** Bilder (C) + Import CSV/Kahoot (D) | **Q5=C+D LOCKED (phased)** |
| A6 | Editor unter `/editor`; Host wählt Quiz | Q6=A Default |
| A7 | Scoring: Basis + Speed-Bonus (Server receive time, Cap) | **Q7=A LOCKED** |
| A8a | Timer: Server start/end → Reveal + Board → **Host Next** | **Q8=A LOCKED** |
| A8b | Auto-Advance (Q8=B) = **later enhancement**, nicht Skeleton/MVP-Kern | **explicit later** |
| A9 | Default 20s | Q9=A Default |
| A10 | Leaderboard nach jeder Frage + Finale | Q10=A Default |
| A11 | Primär `dotnet run`; Docker optional | **Q11=A LOCKED** |
| A12 | SQLite = Quizzes; Live-Session Memory | Q12=A Default |
| A13 | Nickname unique; Host sieht Count; startet bei ≥1 | Q13=A Default |
| A14 | SignalR reconnect + Connection-Count | Q14=A Default |
| A15 | UI Deutsch first | Q15=A Default |
| A16 | Seed-Quiz „Ausbildung Basics“ | Q16=A Default |
| A17 | Ein aktiver Raum / Prozess; `RoomId` im Modell | Q17=C Default |
| A18 | Host spielt nicht mit | Q18=A Default |

---

## Non-Goals

**Nicht MVP / bewusst später oder nie im Kern:**

PowerUps (→ v1.1) · öffentliche Demo · Store-Apps · PWA-Versprechen · Marketplace · Aula-Scale · SSO · Teams · Sound-Packs · Analytics · Feature-Parität Kahoot/ClassQuiz · Multi-Tenant-Auth · Spectator · andere Frage-Typen als Single-Correct-MC · Host Pause/Verlängern · Auto-Advance als Pflicht.

**Nicht im Skeleton, aber Roadmap vor PowerUps (Q5):**

- Bilder in Fragen (C)
- Import CSV/Kahoot (D)

---

## File Structure (Ziel nach Scaffold)

```
trainarena/
├── README.md                 # DE: 2-Minuten Start (dotnet run)
├── LICENSE                   # MIT
├── TrainArena.sln
├── src/
│   └── TrainArena/
│       ├── Program.cs
│       ├── appsettings.json
│       ├── TrainArena.csproj
│       ├── Hubs/
│       │   └── GameHub.cs
│       ├── Game/
│       │   ├── GameSession.cs          # In-memory room state
│       │   ├── GameSessionStore.cs
│       │   ├── Scoring.cs
│       │   └── RoomCodeGenerator.cs
│       ├── Data/
│       │   ├── AppDbContext.cs         # oder Dapper + Schema
│       │   ├── Entities/
│       │   │   ├── Quiz.cs
│       │   │   └── Question.cs
│       │   └── SeedData.cs
│       ├── Contracts/                  # DTOs / Hub payloads
│       │   ├── HubMessages.cs
│       │   └── PlayerView.cs
│       ├── Pages/                      # Host + Editor (Razor) — Q1=A
│       │   ├── Index.cshtml            # Host lobby / control
│       │   ├── Editor/
│       │   └── _Layout.cshtml
│       └── wwwroot/
│           ├── player/
│           │   ├── index.html
│           │   ├── player.js
│           │   └── player.css
│           ├── js/                     # Host page helpers
│           └── lib/signalr/
├── tests/
│   └── TrainArena.Tests/
│       ├── ScoringTests.cs
│       ├── RoomCodeTests.cs
│       └── GameSessionTests.cs
└── docker-compose.yml          # optional, nach spielbarer Runde
```

**Verantwortung kurz:**

| Einheit | Verantwortung |
|---|---|
| `GameHub` | Join/Leave, Host-Commands, Broadcast Events |
| `GameSession` | State-Machine: Lobby → Question → Reveal → Board → Finished |
| `Scoring` | Punkte aus Server-Zeitstempel (Basis + Speed) |
| `AppDbContext` / Entities | Persistente Quizzes |
| `wwwroot/player` | Dünner Client: Events rendern, Answer senden |
| `Pages/` (Razor) | Host-Steuerung + Editor CRUD |

---

## Phasen-Überblick (verbindlich)

| Phase | Deliverable | Bilder/Import? | Testbar wenn… |
|---|---|---|---|
| **0 · Skeleton** | Hub + Join + 1 **Text**-Demo-Frage E2E; Host Razor minimal; Scoring A; Host Next | **Nein** | 2 Browser: Join → antworten → Score |
| **1 · Editor A + Persistenz** | SQLite + CRUD Text-MC (4/1, Zeit) + Seed | **Nein** | Eigenes Quiz anlegen → Runde |
| **2 · Full Round UX** | Multi-Frage, Timer-UI, Reveal, Board, Finale, Host Next | **Nein** | Komplette Runde |
| **3 · Join-Polish** | QR, Reconnect, DE-Copy, README (`dotnet run`) | **Nein** | Happy-Path ohne Erklärung |
| **4 · Editor C+D** | Bilder in Fragen + Import CSV/Kahoot | **Ja** | Quiz mit Bild / Import spielbar |
| **5 · Packaging (optional)** | Docker Compose Docs / später Binary | — | `docker compose up` spielt Runde |
| **Later** | Auto-Advance (Q8=B), PowerUps (v1.1) | — | nach MVP-Kern |

**Reihenfolge-Regel:** Phase 0 darf **nicht** auf C/D warten. Auto-Advance **nicht** vor stabilem Host-Next.

---

## Phase 0 — Skeleton (zuerst bauen)

### Task 0.1: Solution + Web-Projekt + SignalR

**Files:**
- Create: `TrainArena.sln`, `src/TrainArena/TrainArena.csproj`, `Program.cs`, `appsettings.json`
- Create: `tests/TrainArena.Tests/TrainArena.Tests.csproj`

**Interfaces:**
- Produces: lauffähiges ASP.NET-Projekt mit Razor Pages + `MapHub<GameHub>("/hubs/game")`

- [ ] **Step 1: Scaffold**

```bash
dotnet new sln -n TrainArena
dotnet new web -n TrainArena -o src/TrainArena --no-https
dotnet sln add src/TrainArena/TrainArena.csproj
dotnet new xunit -n TrainArena.Tests -o tests/TrainArena.Tests
dotnet sln add tests/TrainArena.Tests/TrainArena.Tests.csproj
dotnet add tests/TrainArena.Tests reference src/TrainArena
```

*(Exact flags an .NET-SDK-Version anpassen; `--no-https` ok für LAN-MVP-Default. Razor Pages aktivieren in `Program.cs`.)*

- [ ] **Step 2: Program.cs — Hub + Razor registrieren**

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddSingleton<GameSessionStore>();
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapRazorPages();
app.MapHub<GameHub>("/hubs/game");
app.MapGet("/health", () => Results.Ok(new { status = "ok", app = "TrainArena" }));
app.Run();
```

- [ ] **Step 3: Commit**

```bash
git add TrainArena.sln src tests
git commit -m "chore: scaffold TrainArena ASP.NET + SignalR skeleton"
```

---

### Task 0.2: Room join + in-memory session

**Files:**
- Create: `Game/GameSession.cs`, `Game/GameSessionStore.cs`, `Game/RoomCodeGenerator.cs`
- Create: `Hubs/GameHub.cs`
- Create: `Contracts/HubMessages.cs`
- Test: `tests/TrainArena.Tests/RoomCodeTests.cs`, `GameSessionTests.cs`

**Interfaces:**
- Produces:
  - `RoomCodeGenerator.Next()` → `string` (6 uppercase)
  - `GameSessionStore.Create(hostConnectionId)` → `GameSession`
  - `GameSession.TryJoin(nickname, connectionId)` → `(bool ok, string? error)`
  - Hub methods: `CreateRoom()`, `JoinRoom(code, nickname)`, events: `RoomCreated`, `PlayerJoined`, `LobbyState`

- [ ] **Step 1: Failing test — Room-Code Länge/Charset**

```csharp
[Fact]
public void Next_ReturnsSixUppercaseAlphanumeric()
{
    var code = new RoomCodeGenerator().Next();
    Assert.Equal(6, code.Length);
    Assert.Matches("^[A-Z0-9]{6}$", code);
}
```

- [ ] **Step 2: Implement `RoomCodeGenerator` + `GameSession` Lobby join (unique nickname)**
- [ ] **Step 3: `GameHub` CreateRoom / JoinRoom + Group `room:{code}`**
- [ ] **Step 4: Manuell** — zwei Connections: Join reflektiert Lobby
- [ ] **Step 5: Commit** `feat: room create/join with nicknames`

---

### Task 0.3: Eine Text-Demo-Frage end-to-end

**Files:**
- Create: `Game/Scoring.cs`, Extend `GameSession` (Question → AcceptAnswer → Reveal)
- Create: `Data/SeedData.cs` (oder hardcoded Text-MC für Skeleton)
- Create: `wwwroot/player/index.html`, `player.js`, `player.css`
- Create: `Pages/Index.cshtml` (+ PageModel) — Host: Code, Start Demo, Board, **Next**
- Test: `ScoringTests.cs`

**Scope-Grenze:** **kein** Image-Upload, **kein** Import, **kein** Auto-Advance.

**Interfaces:**
- Produces:
  - `GameSession.StartQuestion(question)` / `SubmitAnswer(connectionId, optionIndex, serverUtc)`
  - Hub: `StartDemo()`, `SubmitAnswer(int optionIndex)`, `NextQuestion()` (Host), events: `QuestionStarted`, `QuestionEnded`, `ScoreUpdate`, `Leaderboard`
  - Scoring: `int Score(bool correct, TimeSpan elapsed, TimeSpan limit)` — Basis + Speed-Bonus, 0 if wrong

- [ ] **Step 1: Failing scoring tests** (richtig+schnell > richtig+langsam; falsch = 0)
- [ ] **Step 2: Implement Scoring + Session question phase (server end time)**
- [ ] **Step 3: Seed one Text MC question (4 options, 1 correct) — no image fields required yet**
- [ ] **Step 4: Player UI — join form, show question text, 4 buttons, disable after answer / on end**
- [ ] **Step 5: Host Razor UI — Create room, show code, Start demo, Reveal/Board, Host Next**
- [ ] **Step 6: Manual E2E** — Browser A Host, Browser B(+C) Player; eine Text-Frage durchspielen
- [ ] **Step 7: Commit** `feat: demo question live round end-to-end`

**Exit-Kriterium Phase 0:** `dotnet run` → Host öffnen → Code → Player join → Text-Demo-Frage → Punkte → Host Next bereit (auch wenn nur 1 Frage).

---

## Phase 1 — Editor A + SQLite (Text-MC only)

### Task 1.1: Persistenz-Modell

**Files:**
- Create: `Data/Entities/Quiz.cs`, `Question.cs`, `AppDbContext.cs`, migration oder `EnsureCreated` für MVP
- Modify: `Program.cs` DI

**Entities (Phase 1 — Text only; Image-Felder erst Phase 4 optional ergänzen):**

```csharp
public sealed class Quiz
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public List<Question> Questions { get; set; } = new();
}

public sealed class Question
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public string Text { get; set; } = "";
    public string Option0 { get; set; } = "";
    public string Option1 { get; set; } = "";
    public string Option2 { get; set; } = "";
    public string Option3 { get; set; } = "";
    public int CorrectIndex { get; set; } // 0..3
    public int TimeLimitSeconds { get; set; } = 20;
    public int SortOrder { get; set; }
    // Phase 4: string? ImagePath / ImageUrl — nicht jetzt
}
```

- [ ] **Step 1: Tests oder Integration** — Seed speichern/laden
- [ ] **Step 2: EF Core SQLite (bevorzugt) oder Dapper** — eine Wahl beim Implementieren festhalten und README nennen
- [ ] **Step 3: `SeedData` schreibt „Ausbildung Basics“ (3–5 Text-Fragen)**
- [ ] **Step 4: Commit** `feat: sqlite quiz persistence + seed`

---

### Task 1.2: Editor CRUD (A)

**Files:**
- Create: `Pages/Editor/Index.cshtml`, `Edit.cshtml` (+ PageModels)
- Tests: validation (4 options, correctIndex in range)

- [ ] **Step 1: List quizzes / create / edit / delete question (Text-MC)**
- [ ] **Step 2: Validation** — genau eine richtige Antwort; Text nicht leer
- [ ] **Step 3: Host kann Quiz aus Liste wählen beim `CreateRoom(quizId)`**
- [ ] **Step 4: Commit** `feat: quiz editor CRUD text MC`

---

## Phase 2 — Full Round UX (Host Next)

### Task 2.1: Multi-Frage State-Machine

**Files:**
- Modify: `GameSession.cs`, `GameHub.cs`
- States: `Lobby | QuestionActive | Reveal | Leaderboard | Finished`

- [ ] **Step 1: Tests für Transitions** (illegal: Answer in Lobby → reject)
- [ ] **Step 2: Server endet Frage → Reveal + Board; Host ruft `NextQuestion` (kein Auto-Advance)**
- [ ] **Step 3: Broadcast Leaderboard nach jeder Frage; Final screen**
- [ ] **Step 4: Commit** `feat: full round state machine + host next`

### Task 2.2: Timer-UI (Client display only)

**Files:**
- Modify: `player.js`, Host page JS
- Server sends `StartedAtUtc`, `EndsAtUtc`; Client interpolates countdown; **end decided by server event**

- [ ] **Step 1: Client countdown from server timestamps**
- [ ] **Step 2: Late answers after `QuestionEnded` rejected**
- [ ] **Step 3: Commit** `feat: server-authoritative timer display`

---

## Phase 3 — Join-Polish & DE-UX

### Task 3.1: QR-Join

**Files:**
- Host page: QR encoding of `http://{lan-ip}:{port}/player/?code=ABC123`
- Lib: lightweight JS QR lib oder server-generated PNG

- [ ] **Step 1: Show QR + code on Host lobby**
- [ ] **Step 2: Player deep-link pre-fills code**
- [ ] **Step 3: Commit** `feat: QR join for player`

### Task 3.2: Reconnect + Connection-Count

- [ ] **Step 1: SignalR reconnect; re-attach by room+nickname or reconnect token**
- [ ] **Step 2: Host shows „X verbunden“**
- [ ] **Step 3: Commit** `feat: reconnect and lobby connection count`

### Task 3.3: README + DE-Copy

- [ ] **Step 1: README** — Was ist TrainArena, `dotnet run`, Firewall/LAN-Hinweis, Scope/Non-Goals, Roadmap C+D
- [ ] **Step 2: UI-Strings Deutsch**
- [ ] **Step 3: Commit** `docs: README happy path + German UI copy`

**MVP-Kern „shippable“ (Q20=A):** Ende Phase 3 — spielbare Runde + Editor A + Join/QR + Board + README. C+D danach.

---

## Phase 4 — Editor C+D (nach Skeleton, vor PowerUps)

### Task 4.1: Bilder in Fragen

**Files:**
- Modify: `Question` entity (+ optional `ImagePath`), Editor upload/static serve, Player/Host render
- Store images under `wwwroot/uploads/` oder AppData; Größenlimit dokumentieren

- [ ] **Step 1: Optional image per question — upload in Editor**
- [ ] **Step 2: Broadcast image URL in `QuestionStarted` when present**
- [ ] **Step 3: Player/Host show image above options**
- [ ] **Step 4: Commit** `feat: question images`

### Task 4.2: Import CSV / Kahoot

**Files:**
- Create: import service + Editor UI „Import“
- Accept CSV (documented columns) and/or Kahoot export if format documented

- [ ] **Step 1: CSV import → Quiz + 4-option MC rows (map/skip invalid)**
- [ ] **Step 2: Kahoot import path if format feasible; else CSV-only + README note**
- [ ] **Step 3: Commit** `feat: quiz import csv/kahoot`

---

## Phase 5 — Packaging (optional)

### Task 5.1: Docker Compose

- [ ] **Step 1: `Dockerfile` + `docker-compose.yml` (eine Service, Port publish, Volume für SQLite)**
- [ ] **Step 2: README-Abschnitt Docker**
- [ ] **Step 3: Commit** `chore: docker compose for LAN self-host`

Self-contained Binary = nach Bedarf — nicht Gate für MVP-Done (Q11=A, Q20=A).

---

## Later enhancements (nicht jetzt)

| Item | Wann |
|---|---|
| **Auto-Advance (Q8=B)** | Nach stabilem Host-Next; optional Flag |
| **PowerUps** | v1.1 — nach spielbarem MVP inkl. gewünschtem Editor-Umfang |
| Pause/Verlängern Timer | Backlog |

---

## Hub-Protokoll (Contract)

| Direction | Name | Payload (skizze) |
|---|---|---|
| C→S | `CreateRoom` | `{ quizId }` |
| S→C | `RoomCreated` | `{ code }` |
| C→S | `JoinRoom` | `{ code, nickname }` |
| S→C | `LobbyState` | `{ players[], connectedCount }` |
| C→S | `StartGame` / `NextQuestion` | — (Host only) |
| S→C | `QuestionStarted` | `{ index, text, options[4], endsAtUtc, imageUrl? }` |
| C→S | `SubmitAnswer` | `{ optionIndex }` |
| S→C | `AnswerAccepted` | `{ ok }` |
| S→C | `QuestionEnded` | `{ correctIndex }` |
| S→C | `Leaderboard` | `{ entries: [{ nickname, score }] }` |
| S→C | `GameFinished` | `{ entries }` |

Alle Zeiten **UTC Server**. Scoring nur Server. `imageUrl` erst ab Phase 4 befüllen.

---

## Test-Strategie

| Ebene | Was |
|---|---|
| Unit | Scoring, RoomCode, Session transitions |
| Manual E2E | 2–3 Browser / Phone + Laptop im LAN |
| Nicht MVP | Load-Test 200 Clients, Chaos WiFi-Suite |

---

## Open Questions

**Keine BLOCKS mehr offen.** Defaults für Q3–Q4, Q6, Q9–Q10, Q12–Q20 gelten.

Optionale (nicht blockierende) Feinheiten beim Implementieren:

- EF Core vs Dapper — Implementierer wählt, dokumentiert in README
- Kahoot-Import-Genauigkeit — wenn Format zu wild: CSV first, Kahoot best-effort

Vollständige Antwort-Historie: **[mvp-brainstorm.md](./mvp-brainstorm.md)**.

---

## Self-Review

| Check | Ergebnis |
|---|---|
| Spec/MVP-Vertrag Coverage | Editor · Host · Join · Live · Timer · Board · SQLite · ≤40 — Phasen 0–3 (+ C+D in 4) |
| Skeleton first | Phase 0 Text-only; C+D explizit Phase 4 |
| Host Next vs Auto-Advance | A im Kern; B = Later |
| Stack / Q1 | Razor/Minimal + JS — gelockt |
| Non-Goals | PowerUps/Demo/Store etc.; Bilder/Import nicht mehr „ewig raus“ |

---

## Execution Handoff

Plan ist **ready**. Sobald Repo existiert:

1. **Subagent-Driven** (recommended) — ein Subagent pro Task, Review dazwischen  
2. **Inline Execution** — executing-plans mit Checkpoints  

Start bei **Phase 0 / Task 0.1**. Nicht mit Bildern, Import oder Auto-Advance beginnen.

---

*writing-plans skill — plan promoted from DRAFT to answers-locked.*
