<#
.SYNOPSIS
    Éprouve les règles de ciblage des builds locaux, sans rien construire.

.DESCRIPTION
    Ces règles décident ce qu'une machine a le droit de produire. Se tromper ne
    casse aucune compilation : cela livre un artefact silencieusement inutilisable,
    ou refuse un build légitime. D'où des tests, et d'où le fait que le système hôte
    soit un paramètre de ces fonctions plutôt qu'une détection interne — les deux
    sens de la règle se vérifient depuis n'importe quel poste.

    Aucune publication n'est déclenchée : la chaîne réelle est déjà couverte par
    Invoke-NativeSmokeTest.ps1, exécuté par la CI de release sur chaque plateforme.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "../../eng/BuildEnvironment.ps1")

$FailureCount = 0

function Invoke-TestCase {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Body
    )

    try {
        & $Body
        Write-Host "  OK      $Name"
    }
    catch {
        $script:FailureCount++
        Write-Host "  ÉCHEC   $Name — $($_.Exception.Message)"
    }
}

function Assert-Equal {
    param(
        [Parameter(Mandatory)][AllowNull()]$Expected,
        [AllowNull()]$Actual
    )

    if ($Expected -ne $Actual) {
        throw "attendu '$Expected', obtenu '$Actual'"
    }
}

function Assert-Throws {
    param(
        [Parameter(Mandatory)][scriptblock]$Body,
        [Parameter(Mandatory)][string]$ExpectedPattern
    )

    try {
        & $Body
    }
    catch {
        if ($_.Exception.Message -match $ExpectedPattern) {
            return
        }

        throw "message inattendu : $($_.Exception.Message)"
    }

    throw "aucun refus alors qu'un refus était attendu"
}

Write-Host "Règles de ciblage des builds locaux"

Invoke-TestCase "compose l'identifiant d'une cible publiable" {
    $identifier = New-RuntimeIdentifier -OperatingSystem "osx" -Architecture "arm64"
    Assert-Equal -Expected "osx-arm64" -Actual $identifier
}

Invoke-TestCase "refuse une machine hors des cibles publiables" {
    Assert-Throws -ExpectedPattern "non prise en charge" -Body {
        New-RuntimeIdentifier -OperatingSystem "win" -Architecture "arm64"
    }
}

Invoke-TestCase "extrait le système d'exploitation d'une cible" {
    Assert-Equal -Expected "osx" -Actual (Get-RuntimeOperatingSystem "osx-x64")
}

Invoke-TestCase "reconnaît à ce poste une cible publiable" {
    $identifier = Get-HostRuntimeIdentifier
    if ((Get-SupportedRuntimeIdentifiers) -notcontains $identifier) {
        throw "cible hôte '$identifier' hors des cibles publiables"
    }
}

Invoke-TestCase "autorise l'installeur quand l'hôte est le système ciblé" {
    Assert-InstallerSupported -RuntimeIdentifier "win-x64" -HostOperatingSystem "win"
    Assert-InstallerSupported -RuntimeIdentifier "osx-arm64" -HostOperatingSystem "osx"
}

Invoke-TestCase "refuse l'installeur depuis un autre système, et dit où le produire" {
    Assert-Throws -ExpectedPattern "release\.yml" -Body {
        Assert-InstallerSupported -RuntimeIdentifier "osx-arm64" -HostOperatingSystem "win"
    }
}

Invoke-TestCase "refuse l'installeur Linux, jusque sur un hôte Linux" {
    Assert-Throws -ExpectedPattern "archive portable" -Body {
        Assert-InstallerSupported -RuntimeIdentifier "linux-x64" -HostOperatingSystem "linux"
    }
}

Invoke-TestCase "lit une version sémantique dans Directory.Build.props" {
    $version = Get-RepositoryVersion
    if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$') {
        throw "version non sémantique : '$version'"
    }
}

Invoke-TestCase "expose les versions d'outillage épinglées par le dépôt" {
    $pinned = Get-PinnedToolVersions
    foreach ($tool in @("Dotnet", "Node", "Git")) {
        if ([string]::IsNullOrWhiteSpace($pinned[$tool])) {
            throw "version épinglée absente pour '$tool'"
        }
    }
}

if ($FailureCount -gt 0) {
    throw "$FailureCount test(s) de règles de ciblage en échec."
}

Write-Host "Toutes les règles de ciblage sont vérifiées."
