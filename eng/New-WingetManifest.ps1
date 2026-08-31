<#
.SYNOPSIS
    Produces GitHealth's winget manifests from the published installer.

.DESCRIPTION
    winget expects three YAML manifests carrying the version and the SHA-256 sum of
    the exact installer. They are generated at publication time then attached to the
    release: submitting to microsoft/winget-pkgs stays a human action, but it then
    comes down to copying these files.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$InstallerPath,

    [Parameter(Mandatory)]
    [string]$Version,

    [string]$OutputDirectory = (Join-Path $PSScriptRoot "../artifacts/publish/winget"),

    [string]$ReleaseDate = ([DateTimeOffset]::UtcNow.ToString("yyyy-MM-dd"))
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$templateDirectory = Join-Path $PSScriptRoot "winget"
$resolvedInstaller = (Resolve-Path -LiteralPath $InstallerPath).Path
$normalizedVersion = $Version.TrimStart("v")
if ($normalizedVersion -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$') {
    throw "Unexpected version '$Version': winget expects a semantic version."
}

$hash = (Get-FileHash -LiteralPath $resolvedInstaller -Algorithm SHA256).Hash.ToUpperInvariant()
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null
$utf8 = [System.Text.UTF8Encoding]::new($false)

$templates = Get-ChildItem -LiteralPath $templateDirectory -Filter "*.yaml" -File
if ($templates.Count -eq 0) {
    throw "No winget manifest template in '$templateDirectory'."
}

foreach ($template in $templates) {
    $manifest = [System.IO.File]::ReadAllText($template.FullName, $utf8).
        Replace("__VERSION__", $normalizedVersion).
        Replace("__RELEASE_DATE__", $ReleaseDate).
        Replace("__HASH__", $hash)
    if ($manifest -match "__[A-Z_]+__") {
        throw "Manifest '$($template.Name)' still carries an unsubstituted marker."
    }

    $destination = Join-Path $resolvedOutput $template.Name
    [System.IO.File]::WriteAllText($destination, $manifest, $utf8)
    Write-Host "winget manifest ready: $($template.Name)"
}

Write-Host "winget manifests in $resolvedOutput (version $normalizedVersion)"
