<#
.SYNOPSIS
    Produit l'installeur Velopack et les paquets delta de GitHealth.

.DESCRIPTION
    Complète les archives portables, elle ne les remplace pas : Scoop et les
    utilisateurs qui ne veulent pas d'installeur continuent de les consommer.

    Le canal porte l'identifiant de runtime. Velopack déduit sinon le canal du seul
    système d'exploitation, et les deux publications macOS écraseraient leurs flux
    mutuels sous un canal « osx » commun.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PublishDirectory,

    [Parameter(Mandatory)]
    [string]$Version,

    [Parameter(Mandatory)]
    [ValidateSet("win-x64", "osx-x64", "osx-arm64")]
    [string]$RuntimeIdentifier,

    [string]$OutputRoot = (Join-Path $PSScriptRoot "../artifacts/velopack"),

    # Renseigne pour rassembler le lot publiable a cote des archives portables.
    [string]$ReleaseDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$packId = "App.GitHealth"
$packTitle = "GitHealth"
$packAuthors = "Charles LINDECKER"
$vpkVersion = "1.2.0"

function Resolve-VpkCommand {
    param([string]$RequiredVersion)

    # Le paquet est epingle : une version differente changerait le format du flux de
    # releases lu par l'application installee.
    & dotnet tool update --global vpk --version $RequiredVersion | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "L'installation de l'outil vpk $RequiredVersion a echoue."
    }

    $command = Get-Command vpk -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $isWindowsHost = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows
    )
    $executable = if ($isWindowsHost) { "vpk.exe" } else { "vpk" }
    $fallback = Join-Path (Join-Path $HOME ".dotnet/tools") $executable
    if (-not (Test-Path -LiteralPath $fallback -PathType Leaf)) {
        throw "L'outil vpk est introuvable apres installation."
    }

    return $fallback
}

$resolvedPublish = (Resolve-Path -LiteralPath $PublishDirectory).Path
$normalizedVersion = $Version.TrimStart("v")
if ($normalizedVersion -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$') {
    throw "Version '$Version' inattendue : Velopack attend une version semantique."
}

$mainExecutable = if ($RuntimeIdentifier -eq "win-x64") { "githealth.exe" } else { "githealth" }
if (-not (Test-Path -LiteralPath (Join-Path $resolvedPublish $mainExecutable) -PathType Leaf)) {
    throw "La publication $RuntimeIdentifier ne contient pas '$mainExecutable'."
}

# --icon attend un .ico sous Windows et un .icns sous macOS : seule la cible Windows
# dispose aujourd'hui du format attendu.
$iconArguments = @()
if ($RuntimeIdentifier -eq "win-x64") {
    $iconPath = Join-Path $PSScriptRoot "../src/App.GitHealth.Api/githealth.ico"
    if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
        throw "L'icone '$iconPath' est introuvable : les raccourcis seraient sans icone."
    }

    $iconArguments = @("--icon", (Resolve-Path -LiteralPath $iconPath).Path)
}

$destination = Join-Path ([System.IO.Path]::GetFullPath($OutputRoot)) $RuntimeIdentifier
[System.IO.Directory]::CreateDirectory($destination) | Out-Null
$vpk = Resolve-VpkCommand -RequiredVersion $vpkVersion

Write-Host "Empaquetage Velopack de GitHealth $normalizedVersion pour $RuntimeIdentifier..."
& $vpk pack `
    --packId $packId `
    --packVersion $normalizedVersion `
    --packDir $resolvedPublish `
    --mainExe $mainExecutable `
    --packTitle $packTitle `
    --packAuthors $packAuthors `
    --outputDir $destination `
    --channel $RuntimeIdentifier `
    @iconArguments
if ($LASTEXITCODE -ne 0) {
    throw "L'empaquetage Velopack $RuntimeIdentifier a echoue."
}

# GithubSource ne lit que cet actif : sans lui, la release est ignoree en silence.
$releaseFeed = Join-Path $destination "releases.$RuntimeIdentifier.json"
if (-not (Test-Path -LiteralPath $releaseFeed -PathType Leaf)) {
    throw "Le flux de releases '$releaseFeed' est absent : la mise a jour serait muette."
}

$produced = Get-ChildItem -LiteralPath $destination -File
if ($produced.Count -eq 0) {
    throw "L'empaquetage Velopack $RuntimeIdentifier n'a produit aucun fichier."
}

foreach ($file in $produced) {
    Write-Host "Artefact Velopack : $($file.Name)"
}

if ([string]::IsNullOrWhiteSpace($ReleaseDirectory)) {
    return
}

# Ce qui part en release : l'installeur, les paquets, et le flux que l'application
# installee interroge. Le reste (archive portable de vpk, bookkeeping) reste local :
# les archives portables du projet sont deja publiees par Publish-Native.ps1.
$resolvedRelease = [System.IO.Path]::GetFullPath($ReleaseDirectory)
[System.IO.Directory]::CreateDirectory($resolvedRelease) | Out-Null
$publishable = $produced | Where-Object {
    $_.Name -like "App.GitHealth-*.nupkg" -or
    $_.Name -like "App.GitHealth-*-Setup.*" -or
    $_.Name -like "releases.*.json"
}
if ($publishable.Count -eq 0) {
    throw "Aucun artefact Velopack publiable pour $RuntimeIdentifier."
}

foreach ($file in $publishable) {
    Copy-Item -LiteralPath $file.FullName -Destination $resolvedRelease -Force
    Write-Host "Publie : $($file.Name)"
}
