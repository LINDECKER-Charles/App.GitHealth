#!/bin/sh
# Passerelle vers eng/build.ps1, l'unique implémentation des builds locaux.
# Voir eng/README.md pour les prérequis et les niveaux disponibles.
set -eu

script_directory=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

if ! command -v pwsh >/dev/null 2>&1; then
    cat >&2 <<'MESSAGE'
PowerShell 7 est requis par les scripts de build de GitHealth.

  macOS         brew install --cask powershell
  Ubuntu/Debian sudo snap install powershell --classic
  Fedora        sudo dnf install powershell
  Autres        https://learn.microsoft.com/powershell/scripting/install/installing-powershell
MESSAGE
    exit 1
fi

exec pwsh -NoProfile -File "$script_directory/build.ps1" "$@"
