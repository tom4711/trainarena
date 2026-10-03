# TrainArena

Self-hosted Live-Quiz für **Ausbilder ↔ Azubis** (LAN-first, Kahoot-Alternative).

Ein ASP.NET-Core-Prozess: Host/Editor (Razor) · Player (HTML/JS) · SignalR · SQLite.

## Schnellstart

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/TrainArena
```

Dann im Browser (am besten über die **LAN-IP**, nicht nur `localhost`):

| Rolle | URL |
|------|-----|
| Host (Ausbilder) | `http://<lan-ip>:5175/` |
| Spieler (Azubi) | `http://<lan-ip>:5175/player/` oder QR vom Host |
| Editor | `http://<lan-ip>:5175/Editor` |

1. Host: Quiz wählen → **Raum erstellen** → Code/QR zeigen  
2. Spieler: Code scannen/eingeben + Nickname → warten  
3. Host: **Quiz starten** → nach jeder Frage **Weiter** (kein Auto-Advance)

### Firewall / LAN

- Port freigeben (Standard-Dev: **5175**, siehe `launchSettings.json`).
- Host-Seite über `http://<deine-lan-ip>:5175` öffnen, damit der QR-Code für Handys erreichbar ist.
- `localhost` im QR funktioniert auf dem Handy nicht.

### Daten

Quizzes liegen in SQLite (`trainarena.db` neben der App, EF Core). Beim ersten Start wird **Ausbildung Basics** geseedet. Fragebilder liegen unter `wwwroot/uploads/` (max. **2 MB**, JPEG/PNG/WebP/GIF).

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

## Scope (MVP)

- Text-MC-Editor (4 Optionen, 1 richtig) + optionale Bilder
- CSV-/Kahoot-ähnlicher Import
- Host-Raum + Code/QR-Join
- Live-Runde, Server-Timer, Rangliste, Host Next
- Classroom-Größe ~≤40, ein Prozess

## Power-Ups (v1.1)

Beim **Raum erstellen** kann der Host Power-Ups aktivieren und Starter-Anzahlen setzen (50/50, Double, Extra-Zeit, Shield) sowie die Streak-Belohnung (Standard: alle 2 richtigen Antworten).

- **Spieler:** Inventar-Buttons während einer offenen Frage (vor der Antwort); 50/50 blendet zwei falsche Optionen aus.
- **Host:** während der Frage **Team-Boost** (alle Punkte ×2) oder **Zeit +5** (einmal pro Frage, serverseitig).
- Punkte, Inventar und Timer werden **nur auf dem Server** berechnet; die UI zeigt nur den Stand.

## Non-Goals (MVP)

Öffentliche Demo · App-Store · Marketplace · Aula-Scale · SSO · Auto-Advance

## Roadmap danach

- Optional: self-contained Binary  

## Cloud Agent

Das Cursor-Environment ist dashboard-managed. `install` führt
[`scripts/cloud-agent-install.sh`](scripts/cloud-agent-install.sh) aus (.NET SDK 10 in `~/.dotnet`, optional `dotnet restore`).

## Lizenz

MIT — siehe [LICENSE](LICENSE).
