# trainarena

## Cloud Agent development environment

The Cursor Cloud Agent environment for this repository is dashboard-managed. Its
`install` step runs [`scripts/cloud-agent-install.sh`](scripts/cloud-agent-install.sh),
which provisions the toolchain the project targets:

- Installs the **.NET SDK** (LTS channel `10.0`) into `~/.dotnet` if it is not
  already present, and exposes it via `DOTNET_ROOT`/`PATH` for future shells.
- Restores any `*.sln`/`*.slnx`/`*.csproj` found in the repository (a no-op until
  project files are added).
- Leaves the base image's Node.js and Python toolchains untouched.

The script is idempotent and safe to re-run.

## Local run

```bash
dotnet run --project src/TrainArena
```

- Host: `/` — Raum erstellen, Quiz wählen, Runde steuern  
- Player: `/player/` — Code + Nickname  
- Editor: `/Editor` — Text-MC Quizzes (SQLite via EF Core, Datei `trainarena.db`)
