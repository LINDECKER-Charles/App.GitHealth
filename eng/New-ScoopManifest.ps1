<#
.SYNOPSIS
    Produit le manifeste Scoop de GitHealth à partir de l'archive Windows publiée.

.DESCRIPTION
    Scoop installe l'archive portable telle quelle : ni installeur, ni signature. Le
    manifeste doit porter la somme SHA-256 de l'archive exacte, donc il se génère au
    moment de la publication, pas à la main.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ArchivePath,

    [Parameter(Mandatory)]
    [string]$Version,

    [string]$OutputPath = (Join-Path $PSScriptRoot "../artifacts/publish/githealth.json")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$templatePath = Join-Path $PSScriptRoot "scoop/githealth.template.json"
$resolvedArchive = (Resolve-Path -LiteralPath $ArchivePath).Path
$normalizedVersion = $Version.TrimStart("v")
if ($normalizedVersion -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$') {
    throw "Version '$Version' inattendue : le manifeste Scoop attend une version sémantique."
}

$hash = (Get-FileHash -LiteralPath $resolvedArchive -Algorithm SHA256).Hash.ToLowerInvariant()
# Lecture et ecriture en UTF-8 explicite : le manifeste porte des accents, et l'encodage
# par defaut de Get-Content / Set-Content varie entre Windows PowerShell et PowerShell 7.
$utf8 = [System.Text.UTF8Encoding]::new($false)
$manifest = [System.IO.File]::ReadAllText($templatePath, $utf8).
    Replace("__VERSION__", $normalizedVersion).
    Replace("__HASH__", $hash)

# Le manifeste doit rester du JSON valide : une coquille casse l'installation a distance.
$null = ConvertFrom-Json -InputObject $manifest

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($resolvedOutput)
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
[System.IO.File]::WriteAllText($resolvedOutput, $manifest, $utf8)

Write-Host "Manifeste Scoop prêt : $resolvedOutput (version $normalizedVersion)"
