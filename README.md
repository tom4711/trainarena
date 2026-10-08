# TrainArena

Self-hosted Live-Quiz für **Ausbilder ↔ Azubis** (LAN-first, Kahoot-Alternative).

Ein ASP.NET-Core-Prozess: Host/Editor (Razor) · Player (HTML/JS) · SignalR · SQLite.

## Schnellstart

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/TrainArena
```

Dann im Browser — am besten über die **LAN-IP** (nicht nur `localhost`), damit Handys den QR-Link erreichen:

| Rolle | URL |
|------|-----|
| Host (Ausbilder) | `http://<lan-ip>:5175/` |
| Spieler (Azubi) | `http://<lan-ip>:5175/player/` oder QR vom Host |
| Editor | `http://<lan-ip>:5175/Editor` |

1. Host: Quiz wählen → Power-Ups / Auto-Advance optional setzen → **Raum erstellen** → Code/QR zeigen  
2. Spieler: Code scannen oder eingeben + Nickname → warten  
3. Host: **Quiz starten** → nach jeder Frage **Weiter**, oder beim Raumerstellen **Automatisch weiter** (3/5/10 s nach der Rangliste, standardmäßig aus)

### Firewall / LAN / UX

- Port freigeben (Dev-Standard: **5175**, siehe `src/TrainArena/Properties/launchSettings.json`).
- Host-Seite über `http://<deine-lan-ip>:5175` öffnen — `localhost` im QR funktioniert auf dem Handy nicht.
- Host und Player sind für Desktop und Mobile ausgelegt; große Arena-/Power-Up-Effekte nutzen Full-Screen-Overlays (`prefers-reduced-motion` wird respektiert).

### Daten

Quizzes liegen in SQLite (`trainarena.db` neben der App, EF Core). Beim ersten Start wird **Ausbildung Basics** geseedet. Fragebilder unter `wwwroot/uploads/` (max. **2 MB**, JPEG/PNG/WebP/GIF).

```bash
dotnet test TrainArena.sln
```

### Import CSV / Kahoot

Im Editor unter einem Quiz: **Import CSV / Kahoot**.

**TrainArena-CSV** (Header-Zeile Pflicht):

```csv
Question,OptionA,OptionB,OptionC,OptionD,Correct,TimeLimitSeconds
Wie viele Bundesländer?,14,15,16,17,C,20
```

`Correct`: `A`–`D` oder `0`–`3` (auch `1`–`4`). `TimeLimitSeconds` optional (Default 20).

**Kahoot-ähnlich:** Excel-Vorlage als CSV speichern mit Spalten `Question`, `Answer 1`…`Answer 4`, `Time limit`, `Correct answer(s)` (1-basiert). Kein natives `.xlsx` — bei Bedarf vorher als CSV exportieren. Ungültige Zeilen werden übersprungen.

## Docker (LAN self-host)

Voraussetzung: Docker + Docker Compose.

```bash
docker compose up --build -d
```

Dann wie oben über `http://<lan-ip>:5175/` (Host/Player/Editor). Port-Mapping: **5175→8080** im Container.

Persistenz:

| Volume | Inhalt |
|--------|--------|
| `trainarena-data` | SQLite (`TRAINARENA_DB=/data/trainarena.db`) |
| `trainarena-uploads` | Fragebilder unter `/app/wwwroot/uploads` |

Stoppen: `docker compose down` (Volumes bleiben). Volumes mit löschen: `docker compose down -v`.

## Features (aktueller Stand)

- Text-MC-Editor (4 Optionen, 1 richtig) + optionale Bilder
- CSV- / Kahoot-ähnlicher Import
- Host-Raum + Code/QR-Join, Live-Runde, Server-Timer, Rangliste
- **Auto-Advance:** optional beim Raumerstellen — nach der Rangliste automatisch weiter (3/5/10 s; Host kann weiterhin manuell **Weiter** drücken)
- Classroom-Größe ~≤40, ein Prozess
- **Power-Ups** (siehe unten)

## Power-Ups

Beim **Raum erstellen** kann der Host Power-Ups aktivieren, Starter-Anzahlen setzen und die Streak-Belohnung konfigurieren (Standard: alle 2 richtigen Antworten ein zufälliges Spieler-Power-Up).

### Spieler

Während einer offenen Frage (vor der eigenen Antwort), inkl. Inventar-Buttons und Aktivierungs-/Effekt-Animationen:

| Power-Up | Wirkung |
|----------|---------|
| **50/50** | Zwei falsche Optionen ausblenden |
| **Double** | Nächste richtige Antwort ×2 |
| **Extra-Zeit** | +5 s Fragezeit (einmalig pro Frage für den Raum) |
| **Shield** | Blockt den nächsten **Störimpuls** gegen dich und wird dabei verbraucht |
| **Störimpuls** | Angriff auf einen Gegner (Zielwahl oder zufällig): nächste Antwort des Ziels zählt 0 Punkte / kein Streak — außer Schild blockt |

Self-Effects (50/50, Double, Extra-Zeit) sind pro Frage exklusiv; Störimpuls ist davon getrennt und mit ihnen kombinierbar.

### Host (Arena)

Während einer offenen Frage (ein Event pro Frage, serverseitig):

| Power-Up | Wirkung |
|----------|---------|
| **Team-Boost** | Alle Punkte dieser Frage ×1,5 |
| **Zeit +5** | Fragezeit um 5 s verlängern |

### Arena-FX

Wettbewerbs-Power-Ups (Schild / Störimpuls) senden Full-Screen-Overlays an Host und Spieler: Schild aktiv, Schild zerstört, Angriff ausgelöst, Treffer, Geblockt. Punkte, Inventar und Timer werden **nur auf dem Server** berechnet; die UI spiegelt den Stand.

## Scope vs Non-Goals

**Im Scope:** self-host (LAN / Docker), Classroom, Editor, Live-Runde, Auto-Advance, Power-Ups inkl. Arena-FX.

**Nicht im Scope:** öffentliche Demo-/Cloud-Hosting-Instanz · App-Store · Marketplace · Aula-Scale · SSO.

## Roadmap (optional)

- Self-contained Binary (Download ohne Docker/.NET-SDK auf dem Zielrechner)

## CI

GitHub Actions (`.github/workflows/ci.yml`) auf jedem PR und Push nach `main`:

- `dotnet restore` / `build` / `test` (.NET 10)
- `docker build` (Image nur prüfen, kein Registry-Push)

## Cloud Agent

Das Cursor-Environment ist dashboard-managed. `install` führt
[`scripts/cloud-agent-install.sh`](scripts/cloud-agent-install.sh) aus (.NET SDK 10 in `~/.dotnet`, optional `dotnet restore`).

## Lizenz

MIT — siehe [LICENSE](LICENSE).
