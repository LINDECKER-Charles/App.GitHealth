<#
.SYNOPSIS
    Produces GitHealth's Velopack installer and delta packages.

.DESCRIPTION
    It complements the portable archives, it does not replace them: Scoop and the
    users who do not want an installer keep consuming them.

    The channel carries the runtime identifier. Otherwise Velopack derives the
    channel from the operating system alone, and the two macOS publications would
    overwrite each other's feeds under a shared "osx" channel.
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

    # Set to gather the publishable set next to the portable archives.
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

    # The package is pinned: a different version would change the format of the
    # release feed read by the installed application.
    & dotnet tool update --global vpk --version $RequiredVersion | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Installing the vpk $RequiredVersion tool failed."
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
        throw "The vpk tool cannot be found after installation."
    }

    return $fallback
}

$resolvedPublish = (Resolve-Path -LiteralPath $PublishDirectory).Path
$normalizedVersion = $Version.TrimStart("v")
if ($normalizedVersion -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$') {
    throw "Unexpected version '$Version': Velopack expects a semantic version."
}

$mainExecutable = if ($RuntimeIdentifier -eq "win-x64") { "githealth.exe" } else { "githealth" }
if (-not (Test-Path -LiteralPath (Join-Path $resolvedPublish $mainExecutable) -PathType Leaf)) {
    throw "The $RuntimeIdentifier publication does not contain '$mainExecutable'."
}

# --icon expects a .ico on Windows and a .icns on macOS: only the Windows target
# has the expected format today.
$iconArguments = @()
if ($RuntimeIdentifier -eq "win-x64") {
    $iconPath = Join-Path $PSScriptRoot "../src/App.GitHealth.Api/githealth.ico"
    if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
        throw "Icon '$iconPath' cannot be found: the shortcuts would have no icon."
    }

    $iconArguments = @("--icon", (Resolve-Path -LiteralPath $iconPath).Path)
}

$destination = Join-Path ([System.IO.Path]::GetFullPath($OutputRoot)) $RuntimeIdentifier
[System.IO.Directory]::CreateDirectory($destination) | Out-Null
$vpk = Resolve-VpkCommand -RequiredVersion $vpkVersion

Write-Host "Velopack packaging of GitHealth $normalizedVersion for $RuntimeIdentifier..."
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
    throw "Velopack packaging for $RuntimeIdentifier failed."
}

# GithubSource reads only this asset: without it, the release is silently ignored.
$releaseFeed = Join-Path $destination "releases.$RuntimeIdentifier.json"
if (-not (Test-Path -LiteralPath $releaseFeed -PathType Leaf)) {
    throw "Release feed '$releaseFeed' is missing: the update would stay silent."
}

$produced = Get-ChildItem -LiteralPath $destination -File
if ($produced.Count -eq 0) {
    throw "Velopack packaging for $RuntimeIdentifier produced no file."
}

foreach ($file in $produced) {
    Write-Host "Velopack artefact: $($file.Name)"
}

if ([string]::IsNullOrWhiteSpace($ReleaseDirectory)) {
    return
}

# What goes out in the release: the installer, the packages, and the feed the
# installed application queries. The rest (vpk's portable archive, bookkeeping) stays
# local: the project's portable archives are already published by Publish-Native.ps1.
$resolvedRelease = [System.IO.Path]::GetFullPath($ReleaseDirectory)
[System.IO.Directory]::CreateDirectory($resolvedRelease) | Out-Null
$publishable = $produced | Where-Object {
    $_.Name -like "App.GitHealth-*.nupkg" -or
    $_.Name -like "App.GitHealth-*-Setup.*" -or
    $_.Name -like "releases.*.json"
}
if ($publishable.Count -eq 0) {
    throw "No publishable Velopack artefact for $RuntimeIdentifier."
}

foreach ($file in $publishable) {
    Copy-Item -LiteralPath $file.FullName -Destination $resolvedRelease -Force
    Write-Host "Published: $($file.Name)"
}
