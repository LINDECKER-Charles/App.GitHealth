<#
.SYNOPSIS
    Produces GitHealth's Scoop manifest from the published Windows archive.

.DESCRIPTION
    Scoop installs the portable archive as it is: no installer, no signature. The
    manifest must carry the SHA-256 sum of the exact archive, so it is generated at
    publication time, not by hand.
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
    throw "Unexpected version '$Version': the Scoop manifest expects a semantic version."
}

$hash = (Get-FileHash -LiteralPath $resolvedArchive -Algorithm SHA256).Hash.ToLowerInvariant()
# Explicit UTF-8 on read and on write: the default encoding of Get-Content and
# Set-Content varies between Windows PowerShell and PowerShell 7.
$utf8 = [System.Text.UTF8Encoding]::new($false)
$manifest = [System.IO.File]::ReadAllText($templatePath, $utf8).
    Replace("__VERSION__", $normalizedVersion).
    Replace("__HASH__", $hash)

# The manifest must stay valid JSON: a typo breaks the installation remotely.
$null = ConvertFrom-Json -InputObject $manifest

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($resolvedOutput)
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
[System.IO.File]::WriteAllText($resolvedOutput, $manifest, $utf8)

Write-Host "Scoop manifest ready: $resolvedOutput (version $normalizedVersion)"
