<#
.SYNOPSIS
    Exercises the local build targeting rules, without building anything.

.DESCRIPTION
    These rules decide what a machine is allowed to produce. Getting them wrong
    breaks no compilation: it ships a silently unusable artefact, or refuses a
    legitimate build. Hence tests, and hence the fact that the host system is a
    parameter of these functions rather than an internal detection — both sides
    of the rule can be verified from any machine.

    No publication is triggered: the real chain is already covered by
    Invoke-NativeSmokeTest.ps1, run by the release CI on every platform.
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
        Write-Host "  FAILED  $Name — $($_.Exception.Message)"
    }
}

function Assert-Equal {
    param(
        [Parameter(Mandatory)][AllowNull()]$Expected,
        [AllowNull()]$Actual
    )

    if ($Expected -ne $Actual) {
        throw "expected '$Expected', got '$Actual'"
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

        throw "unexpected message: $($_.Exception.Message)"
    }

    throw "no rejection although a rejection was expected"
}

Write-Host "Local build targeting rules"

Invoke-TestCase "composes the identifier of a publishable target" {
    $identifier = New-RuntimeIdentifier -OperatingSystem "osx" -Architecture "arm64"
    Assert-Equal -Expected "osx-arm64" -Actual $identifier
}

Invoke-TestCase "rejects a machine outside the publishable targets" {
    Assert-Throws -ExpectedPattern "is not supported" -Body {
        New-RuntimeIdentifier -OperatingSystem "win" -Architecture "arm64"
    }
}

Invoke-TestCase "extracts the operating system of a target" {
    Assert-Equal -Expected "osx" -Actual (Get-RuntimeOperatingSystem "osx-x64")
}

Invoke-TestCase "recognises this machine as a publishable target" {
    $identifier = Get-HostRuntimeIdentifier
    if ((Get-SupportedRuntimeIdentifiers) -notcontains $identifier) {
        throw "host target '$identifier' outside the publishable targets"
    }
}

Invoke-TestCase "allows the installer when the host is the targeted system" {
    Assert-InstallerSupported -RuntimeIdentifier "win-x64" -HostOperatingSystem "win"
    Assert-InstallerSupported -RuntimeIdentifier "osx-arm64" -HostOperatingSystem "osx"
}

Invoke-TestCase "rejects the installer from another system, and says where to produce it" {
    Assert-Throws -ExpectedPattern "release\.yml" -Body {
        Assert-InstallerSupported -RuntimeIdentifier "osx-arm64" -HostOperatingSystem "win"
    }
}

Invoke-TestCase "rejects the Linux installer, even on a Linux host" {
    Assert-Throws -ExpectedPattern "portable archive" -Body {
        Assert-InstallerSupported -RuntimeIdentifier "linux-x64" -HostOperatingSystem "linux"
    }
}

Invoke-TestCase "reads a semantic version from Directory.Build.props" {
    $version = Get-RepositoryVersion
    if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$') {
        throw "non-semantic version: '$version'"
    }
}

Invoke-TestCase "exposes the tooling versions pinned by the repository" {
    $pinned = Get-PinnedToolVersions
    foreach ($tool in @("Dotnet", "Node", "Git")) {
        if ([string]::IsNullOrWhiteSpace($pinned[$tool])) {
            throw "pinned version missing for '$tool'"
        }
    }
}

if ($FailureCount -gt 0) {
    throw "$FailureCount build targeting rule test(s) failed."
}

Write-Host "All build targeting rules are verified."
