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
