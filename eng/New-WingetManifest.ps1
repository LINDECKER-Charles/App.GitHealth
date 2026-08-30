<#
.SYNOPSIS
    Produit les manifestes winget de GitHealth a partir de l'installeur publie.

.DESCRIPTION
    winget attend trois manifestes YAML portant la version et la somme SHA-256 de
    l'installeur exact. Ils sont generes a la publication puis joints a la release :
    la soumission a microsoft/winget-pkgs reste une action humaine, mais elle se
    reduit alors a copier ces fichiers.
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
    throw "Version '$Version' inattendue : winget attend une version semantique."
}

$hash = (Get-FileHash -LiteralPath $resolvedInstaller -Algorithm SHA256).Hash.ToUpperInvariant()
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null
$utf8 = [System.Text.UTF8Encoding]::new($false)

$templates = Get-ChildItem -LiteralPath $templateDirectory -Filter "*.yaml" -File
if ($templates.Count -eq 0) {
    throw "Aucun modele de manifeste winget dans '$templateDirectory'."
}

foreach ($template in $templates) {
    $manifest = [System.IO.File]::ReadAllText($template.FullName, $utf8).
        Replace("__VERSION__", $normalizedVersion).
        Replace("__RELEASE_DATE__", $ReleaseDate).
        Replace("__HASH__", $hash)
    if ($manifest -match "__[A-Z_]+__") {
        throw "Le manifeste '$($template.Name)' porte encore un marqueur non substitue."
    }

    $destination = Join-Path $resolvedOutput $template.Name
    [System.IO.File]::WriteAllText($destination, $manifest, $utf8)
    Write-Host "Manifeste winget pret : $($template.Name)"
}

Write-Host "Manifestes winget dans $resolvedOutput (version $normalizedVersion)"
