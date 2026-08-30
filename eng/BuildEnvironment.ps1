<#
.SYNOPSIS
    Décrit la machine hôte et son outillage pour les builds locaux de GitHealth.

.DESCRIPTION
    Destiné au dot-sourcing par eng/build.ps1. Il répond à deux questions sans en
    tirer aucune conséquence — « pour quelle cible cette machine peut-elle
    construire ? » et « qu'est-ce qui manque ? » ; les décisions restent au
    dispatcher.

    Compatible Windows PowerShell 5.1 et PowerShell 7. Les variables automatiques
    $IsWindows / $IsMacOS / $IsLinux, l'opérateur ternaire et Join-Path à plus de
    deux segments n'existent qu'à partir de PowerShell 6 : ce fichier les évite.

    Le fichier porte une nomenclature d'octets UTF-8. Sans elle, PowerShell 5.1 lit
    les .ps1 dans la page de codes ANSI du poste et rend les accents illisibles.
#>

Set-StrictMode -Version Latest

# Les cibles publiables par eng/Publish-Native.ps1, seule source de vérité.
$SupportedRuntimeIdentifiers = @("win-x64", "osx-x64", "osx-arm64", "linux-x64")

# Version minimale documentée dans .github/CONTRIBUTING.md.
$MinimumGitVersion = "2.38"

function Get-RepositoryRoot {
    return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
}

function Get-SupportedRuntimeIdentifiers {
    return $SupportedRuntimeIdentifiers
}

function Get-MinimumGitVersion {
    return $MinimumGitVersion
}

function Get-HostOperatingSystem {
    $runtime = [System.Runtime.InteropServices.RuntimeInformation]
    $platform = [System.Runtime.InteropServices.OSPlatform]
    if ($runtime::IsOSPlatform($platform::Windows)) { return "win" }
    if ($runtime::IsOSPlatform($platform::OSX)) { return "osx" }
    if ($runtime::IsOSPlatform($platform::Linux)) { return "linux" }

    throw "Système hôte non reconnu : aucun identifiant de runtime ne lui correspond."
}

function Get-HostArchitecture {
    $architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    switch ($architecture.ToString()) {
        "X64" { return "x64" }
        "Arm64" { return "arm64" }
        default { return $architecture.ToString().ToLowerInvariant() }
    }
}

<#
.SYNOPSIS
    Compose un identifiant de runtime et le confronte aux cibles prises en charge.
#>
function New-RuntimeIdentifier {
    param(
        [Parameter(Mandatory)][string]$OperatingSystem,
        [Parameter(Mandatory)][string]$Architecture
    )

    $identifier = "$OperatingSystem-$Architecture"
    if ($SupportedRuntimeIdentifiers -contains $identifier) {
        return $identifier
    }

    $available = $SupportedRuntimeIdentifiers -join ", "
    throw @"
Cible '$identifier' non prise en charge par GitHealth.
Cibles disponibles : $available.
Sur une machine hors de cette liste, désigner explicitement une cible proche —
par exemple -Runtime win-x64 sur Windows ARM, qui s'exécute alors en émulation.
"@
}

function Get-HostRuntimeIdentifier {
    return New-RuntimeIdentifier `
        -OperatingSystem (Get-HostOperatingSystem) `
        -Architecture (Get-HostArchitecture)
}

function Get-RuntimeOperatingSystem {
    param([Parameter(Mandatory)][string]$RuntimeIdentifier)

    return $RuntimeIdentifier.Split("-")[0]
}

<#
.SYNOPSIS
    Vérifie qu'un installeur Velopack peut être produit ici, pour cette cible.

.DESCRIPTION
    Deux refus, pour deux raisons distinctes. Linux n'a pas d'installeur par
    décision produit (docs/DESKTOP_PLAN.md) : la distribution y passe par l'archive
    portable. Et vpk s'appuie sur l'outillage du système ciblé — un bundle .app se
    construit sur macOS, un Setup.exe sur Windows. Échouer ici avec une phrase
    claire vaut mieux que laisser vpk échouer plus loin, ou pire, produire un
    paquet inutilisable.

    L'hôte est un paramètre plutôt qu'une détection interne : la règle devient
    vérifiable sans dépendre de la machine qui exécute le test.
#>
function Assert-InstallerSupported {
    param(
        [Parameter(Mandatory)][string]$RuntimeIdentifier,
        [string]$HostOperatingSystem = (Get-HostOperatingSystem)
    )

    $target = Get-RuntimeOperatingSystem $RuntimeIdentifier
    if ($target -eq "linux") {
        throw @"
Linux n'a pas d'installeur : sa distribution est l'archive portable produite par
'build publish'. Décision documentée dans docs/DESKTOP_PLAN.md, section 2.
"@
    }

    if ($target -eq $HostOperatingSystem) {
        return
    }

    throw @"
Un installeur $RuntimeIdentifier ne peut pas être produit depuis un hôte
'$HostOperatingSystem' : Velopack s'appuie sur l'outillage du système ciblé.
Passer par .github/workflows/release.yml, dont la matrice construit chaque cible
sur son propre runner.
"@
}

<#
.SYNOPSIS
    Lit les versions d'outillage épinglées par le dépôt.
#>
function Get-PinnedToolVersions {
    $root = Get-RepositoryRoot
    $globalJson = Get-Content -LiteralPath (Join-Path $root "global.json") -Raw |
        ConvertFrom-Json
    $nodeVersion = (Get-Content -LiteralPath (Join-Path $root ".nvmrc") -Raw).Trim()

    return @{
        Dotnet = $globalJson.sdk.version
        Node = $nodeVersion
        Git = $MinimumGitVersion
    }
}

<#
.SYNOPSIS
    Lit la version du produit là où MSBuild la lit, pour ne pas en inventer une seconde.
#>
function Get-RepositoryVersion {
    $propsPath = Join-Path (Get-RepositoryRoot) "Directory.Build.props"
    [xml]$props = Get-Content -LiteralPath $propsPath -Raw
    $prefix = $props.SelectSingleNode("/Project/PropertyGroup/VersionPrefix")
    if ($null -eq $prefix) {
        throw "VersionPrefix est absent de '$propsPath'."
    }

    $suffix = $props.SelectSingleNode("/Project/PropertyGroup/VersionSuffix")
    if ($null -eq $suffix -or [string]::IsNullOrWhiteSpace($suffix.InnerText)) {
        return $prefix.InnerText
    }

    return "$($prefix.InnerText)-$($suffix.InnerText)"
}

<#
.SYNOPSIS
    Renvoie la version d'un outil du PATH, ou $null s'il est absent ou muet.
#>
function Get-InstalledToolVersion {
    param(
        [Parameter(Mandatory)][string]$Name,
        [string[]]$Arguments = @("--version")
    )

    if ($null -eq (Get-Command $Name -ErrorAction SilentlyContinue)) {
        return $null
    }

    try {
        $output = & $Name @Arguments
    }
    catch {
        return $null
    }

    $firstLine = @($output) | Select-Object -First 1
    if ($null -eq $firstLine) {
        return $null
    }

    # « git version 2.51.0 » et « v24.20.0 » portent la version au milieu du texte.
    $version = [regex]::Match([string]$firstLine, '\d+\.\d+\.\d+[0-9A-Za-z.+-]*')
    if ($version.Success) {
        return $version.Value
    }

    return ([string]$firstLine).Trim()
}

$PrerequisiteStatusOk = "OK"
$PrerequisiteStatusMismatch = "Écart"
$PrerequisiteStatusMissing = "Absent"

<#
.SYNOPSIS
    Décrit l'outillage attendu par un build local.

.DESCRIPTION
    VersionArguments à $null signale un outil qui ne sait pas dire sa version : seule
    sa présence est constatée. IsPinned distingue une version verrouillée par le
    dépôt, dont tout écart mérite un signalement, d'un simple minimum indicatif.
#>
function Get-PrerequisiteDefinitions {
    $pinned = Get-PinnedToolVersions
    return @(
        @{ Label = "SDK .NET"; Command = "dotnet"; Expected = $pinned.Dotnet
            VersionArguments = @("--version"); IsRequired = $true; IsPinned = $true }
        @{ Label = "Node.js"; Command = "node"; Expected = $pinned.Node
            VersionArguments = @("--version"); IsRequired = $true; IsPinned = $true }
        @{ Label = "npm"; Command = "npm"; Expected = "—"
            VersionArguments = @("--version"); IsRequired = $true; IsPinned = $false }
        @{ Label = "Git"; Command = "git"; Expected = "$($pinned.Git) ou plus récent"
            VersionArguments = @("--version"); IsRequired = $true; IsPinned = $false }
        @{ Label = "Velopack (vpk)"; Command = "vpk"; Expected = "installé à la demande"
            VersionArguments = $null; IsRequired = $false; IsPinned = $false }
    )
}

function Get-ToolPresence {
    param([Parameter(Mandatory)][hashtable]$Tool)

    if ($null -ne $Tool.VersionArguments) {
        return Get-InstalledToolVersion -Name $Tool.Command -Arguments $Tool.VersionArguments
    }

    if ($null -eq (Get-Command $Tool.Command -ErrorAction SilentlyContinue)) {
        return $null
    }

    return "présent"
}

function New-PrerequisiteRow {
    param([Parameter(Mandatory)][hashtable]$Tool)

    $installed = Get-ToolPresence -Tool $Tool
    $status = $PrerequisiteStatusOk
    if ([string]::IsNullOrWhiteSpace($installed)) {
        $status = $PrerequisiteStatusMissing
        $installed = "—"
    }
    elseif ($Tool.IsPinned -and $installed -ne $Tool.Expected) {
        $status = $PrerequisiteStatusMismatch
    }

    return [pscustomobject]@{
        Outil = $Tool.Label
        Attendu = $Tool.Expected
        Trouvé = $installed
        Statut = $status
        Requis = $Tool.IsRequired
    }
}

function Get-PrerequisiteReport {
    return @(Get-PrerequisiteDefinitions | ForEach-Object { New-PrerequisiteRow -Tool $_ })
}

function Get-MissingRequiredTools {
    param([Parameter(Mandatory)][object[]]$Report)

    return @($Report | Where-Object { $_.Requis -and $_.Statut -eq $PrerequisiteStatusMissing })
}
