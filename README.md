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

Quizzes liegen in SQLite (`trainarena.db` neben der App, EF Core). Beim ersten Start wird **Ausbildung Basics** geseedet.

```bash
dotnet test TrainArena.sln
```

## Scope (MVP)

- Text-MC-Editor (4 Optionen, 1 richtig)
- Host-Raum + Code/QR-Join
- Live-Runde, Server-Timer, Rangliste, Host Next
- Classroom-Größe ~≤40, ein Prozess

## Non-Goals (MVP)

PowerUps (→ v1.1) · öffentliche Demo · App-Store · Marketplace · Aula-Scale · SSO · Auto-Advance

## Roadmap danach

- **Phase 4:** Bilder in Fragen + Import CSV/Kahoot  
- **v1.1:** PowerUps  
- Optional: Docker Compose / self-contained Binary  

## Cloud Agent

Das Cursor-Environment ist dashboard-managed. `install` führt
[`scripts/cloud-agent-install.sh`](scripts/cloud-agent-install.sh) aus (.NET SDK 10 in `~/.dotnet`, optional `dotnet restore`).

## Lizenz

MIT — siehe [LICENSE](LICENSE).
