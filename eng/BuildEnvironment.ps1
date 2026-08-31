<#
.SYNOPSIS
    Describes the host machine and its toolchain for GitHealth's local builds.

.DESCRIPTION
    Meant to be dot-sourced by eng/build.ps1. It answers two questions without
    drawing any conclusion from them — "which target can this machine build for?"
    and "what is missing?" — the decisions stay in the dispatcher.

    Compatible with Windows PowerShell 5.1 and PowerShell 7. The automatic variables
    $IsWindows / $IsMacOS / $IsLinux, the ternary operator and Join-Path with more
    than two segments only exist from PowerShell 6 on: this file avoids them.

    The file carries a UTF-8 byte order mark. Without it, PowerShell 5.1 reads the
    .ps1 files in the machine's ANSI code page and mangles every non-ASCII character.
#>

Set-StrictMode -Version Latest

# The targets eng/Publish-Native.ps1 can publish, the single source of truth.
$SupportedRuntimeIdentifiers = @("win-x64", "osx-x64", "osx-arm64", "linux-x64")

# Minimum version documented in .github/CONTRIBUTING.md.
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

    throw "Host system not recognised: no runtime identifier matches it."
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
    Composes a runtime identifier and checks it against the supported targets.
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
Target '$identifier' is not supported by GitHealth.
Available targets: $available.
On a machine outside this list, name a close target explicitly — for example
-Runtime win-x64 on Windows ARM, which then runs under emulation.
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
    Checks that a Velopack installer can be produced here, for this target.

.DESCRIPTION
    Two refusals, for two distinct reasons. Linux has no installer by product
    decision (docs/DESKTOP_PLAN.md): distribution there goes through the portable
    archive. And vpk relies on the target system's toolchain — an .app bundle is
    built on macOS, a Setup.exe on Windows. Failing here with a clear sentence
    beats letting vpk fail further along, or worse, producing an unusable package.

    The host is a parameter rather than an internal detection: the rule then becomes
    verifiable without depending on the machine that runs the test.
#>
function Assert-InstallerSupported {
    param(
        [Parameter(Mandatory)][string]$RuntimeIdentifier,
        [string]$HostOperatingSystem = (Get-HostOperatingSystem)
    )

    $target = Get-RuntimeOperatingSystem $RuntimeIdentifier
    if ($target -eq "linux") {
        throw @"
There is no installer on Linux: distribution there goes through the portable archive
produced by 'build publish'. Decision documented in docs/DESKTOP_PLAN.md, section 2.
"@
    }

    if ($target -eq $HostOperatingSystem) {
        return
    }

    throw @"
A $RuntimeIdentifier installer cannot be produced from a '$HostOperatingSystem'
host: Velopack relies on the target system's toolchain. Go through
.github/workflows/release.yml, whose matrix builds each target on its own runner.
"@
}

<#
.SYNOPSIS
    Reads the tool versions pinned by the repository.
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
    Reads the product version where MSBuild reads it, so a second one is never invented.
#>
function Get-RepositoryVersion {
    $propsPath = Join-Path (Get-RepositoryRoot) "Directory.Build.props"
    [xml]$props = Get-Content -LiteralPath $propsPath -Raw
    $prefix = $props.SelectSingleNode("/Project/PropertyGroup/VersionPrefix")
    if ($null -eq $prefix) {
        throw "VersionPrefix is missing from '$propsPath'."
    }

    $suffix = $props.SelectSingleNode("/Project/PropertyGroup/VersionSuffix")
    if ($null -eq $suffix -or [string]::IsNullOrWhiteSpace($suffix.InnerText)) {
        return $prefix.InnerText
    }

    return "$($prefix.InnerText)-$($suffix.InnerText)"
}

<#
.SYNOPSIS
    Returns the version of a tool on the PATH, or $null if it is missing or silent.
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

    # "git version 2.51.0" and "v24.20.0" carry the version in the middle of the text.
    $version = [regex]::Match([string]$firstLine, '\d+\.\d+\.\d+[0-9A-Za-z.+-]*')
    if ($version.Success) {
        return $version.Value
    }

    return ([string]$firstLine).Trim()
}

$PrerequisiteStatusOk = "OK"
$PrerequisiteStatusMismatch = "Mismatch"
$PrerequisiteStatusMissing = "Missing"

<#
.SYNOPSIS
    Describes the toolchain a local build expects.

.DESCRIPTION
    VersionArguments set to $null marks a tool that cannot report its version: only
    its presence is checked. IsPinned separates a version locked by the repository,
    where any difference deserves a signal, from a plain indicative minimum.
#>
function Get-PrerequisiteDefinitions {
    $pinned = Get-PinnedToolVersions
    return @(
        @{ Label = ".NET SDK"; Command = "dotnet"; Expected = $pinned.Dotnet
            VersionArguments = @("--version"); IsRequired = $true; IsPinned = $true }
        @{ Label = "Node.js"; Command = "node"; Expected = $pinned.Node
            VersionArguments = @("--version"); IsRequired = $true; IsPinned = $true }
        @{ Label = "npm"; Command = "npm"; Expected = "—"
            VersionArguments = @("--version"); IsRequired = $true; IsPinned = $false }
        @{ Label = "Git"; Command = "git"; Expected = "$($pinned.Git) or newer"
            VersionArguments = @("--version"); IsRequired = $true; IsPinned = $false }
        @{ Label = "Velopack (vpk)"; Command = "vpk"; Expected = "installed on demand"
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

    return "present"
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
        Tool = $Tool.Label
        Expected = $Tool.Expected
        Found = $installed
        Status = $status
        Required = $Tool.IsRequired
    }
}

function Get-PrerequisiteReport {
    return @(Get-PrerequisiteDefinitions | ForEach-Object { New-PrerequisiteRow -Tool $_ })
}

function Get-MissingRequiredTools {
    param([Parameter(Mandatory)][object[]]$Report)

    return @($Report | Where-Object { $_.Required -and $_.Status -eq $PrerequisiteStatusMissing })
}
