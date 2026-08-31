#!/bin/sh
# Gateway to eng/build.ps1, the single implementation of local builds.
# See eng/README.md for the prerequisites and the available levels.
set -eu

script_directory=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

if ! command -v pwsh >/dev/null 2>&1; then
    cat >&2 <<'MESSAGE'
PowerShell 7 is required by GitHealth's build scripts.

  macOS         brew install --cask powershell
  Ubuntu/Debian sudo snap install powershell --classic
  Fedora        sudo dnf install powershell
  Other         https://learn.microsoft.com/powershell/scripting/install/installing-powershell
MESSAGE
    exit 1
fi

exec pwsh -NoProfile -File "$script_directory/build.ps1" "$@"
