#!/usr/bin/env bash
# Cloud Agent install script for the trainarena environment.
#
# Idempotent bootstrap that provisions the .NET SDK (the repository targets
# .NET/Visual Studio tooling) without disturbing the Node.js/Python toolchains
# already present in the base image. Safe to run repeatedly and against a
# cached or snapshotted filesystem.
set -euo pipefail

DOTNET_CHANNEL="10.0"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"

log() { printf '[cloud-agent-install] %s\n' "$1"; }

install_dotnet() {
  if command -v dotnet >/dev/null 2>&1; then
    log "dotnet already on PATH: $(dotnet --version)"
    return
  fi
  if [ -x "$DOTNET_ROOT/dotnet" ]; then
    log "dotnet already installed at $DOTNET_ROOT"
    return
  fi

  log "Installing .NET SDK (channel $DOTNET_CHANNEL) into $DOTNET_ROOT"
  local script
  script="$(mktemp)"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$script"
  bash "$script" --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_ROOT"
  rm -f "$script"
}

persist_env() {
  # Make dotnet discoverable in future interactive/non-login agent shells.
  local marker="# >>> trainarena dotnet env >>>"
  if ! grep -qF "$marker" "$HOME/.bashrc" 2>/dev/null; then
    {
      printf '\n%s\n' "$marker"
      printf 'export DOTNET_ROOT="%s"\n' "$DOTNET_ROOT"
      printf 'export PATH="$DOTNET_ROOT:$PATH"\n'
      printf 'export DOTNET_CLI_TELEMETRY_OPTOUT=1\n'
      printf 'export DOTNET_NOLOGO=1\n'
      printf '# <<< trainarena dotnet env <<<\n'
    } >>"$HOME/.bashrc"
    log "Added dotnet environment exports to ~/.bashrc"
  fi
}

restore_if_project_exists() {
  export DOTNET_ROOT
  export PATH="$DOTNET_ROOT:$PATH"
  export DOTNET_CLI_TELEMETRY_OPTOUT=1
  export DOTNET_NOLOGO=1

  # Prefer solution files; fall back to individual projects. Explicitly target
  # each discovered file so restore works regardless of directory layout.
  local -a solutions projects
  mapfile -t solutions < <(find . -path ./.git -prune -o \( -name '*.sln' -o -name '*.slnx' \) -print 2>/dev/null)
  mapfile -t projects  < <(find . -path ./.git -prune -o -name '*.csproj' -print 2>/dev/null)

  if [ "${#solutions[@]}" -gt 0 ]; then
    log "Found ${#solutions[@]} solution file(s); running dotnet restore"
    local sln
    for sln in "${solutions[@]}"; do dotnet restore "$sln"; done
  elif [ "${#projects[@]}" -gt 0 ]; then
    log "Found ${#projects[@]} project file(s); running dotnet restore"
    local proj
    for proj in "${projects[@]}"; do dotnet restore "$proj"; done
  else
    log "No solution/project files yet; skipping restore"
  fi
}

install_dotnet
persist_env
restore_if_project_exists

log "Done. dotnet version: $("$DOTNET_ROOT/dotnet" --version 2>/dev/null || echo 'unavailable')"
