<#
.SYNOPSIS
    Single entry point for GitHealth's local builds, on Windows, macOS and Linux.

.DESCRIPTION
    One level per intent, from the shortest to the most complete:

      check      reports the machine's toolchain and the target it can produce
      dev        API and Angular interface live, reloading included
      publish    self-contained executable, as it is distributed
      run        runs the result of "publish"
      installer  Velopack installer and update feed

    The script implements no publication step of its own: it delegates to
    eng/Publish-Native.ps1 and eng/New-VelopackRelease.ps1, which CI calls too.
    A local build and a release build therefore follow the same path.

    Compatible with Windows PowerShell 5.1 and PowerShell 7; see eng/README.md.

.EXAMPLE
    ./eng/build.sh check

.EXAMPLE
    ./eng/build.sh publish
    ./eng/build.sh run --repo ~/Dev/MyRepository

.EXAMPLE
    eng\build.cmd installer
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("check", "dev", "publish", "run", "installer")]
    [string]$Level = "check",

    # Empty: the host machine's target.
    [ValidateSet("win-x64", "osx-x64", "osx-arm64", "linux-x64")]
    [string]$Runtime,

    # Empty: the version carried by Directory.Build.props.
    [string]$Version,

    # Anything not recognised above goes to the "run" level, on to the application.
    # PowerShell -File does not know the "--" separator: do not use it, the arguments
    # follow the level directly.
    [Parameter(ValueFromRemainingArguments)]
    [string[]]$ApplicationArguments = @()
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "BuildEnvironment.ps1")

$ApiProjectPath = "src/App.GitHealth.Api"
$FrontendProjectPath = "src/App.GitHealth.Web"
$AngularCliEntryPoint = "node_modules/@angular/cli/bin/ng.js"
$PublishRootPath = "artifacts/publish"

function Resolve-TargetRuntime {
    if (-not [string]::IsNullOrWhiteSpace($Runtime)) {
        return $Runtime
    }

    return Get-HostRuntimeIdentifier
}

function Get-PublishDirectory {
    param([Parameter(Mandatory)][string]$RuntimeIdentifier)

    $root = Join-Path (Get-RepositoryRoot) $PublishRootPath
    return Join-Path $root $RuntimeIdentifier
}

function Get-PublishedExecutable {
    param([Parameter(Mandatory)][string]$RuntimeIdentifier)

    $name = "githealth"
    if ((Get-RuntimeOperatingSystem $RuntimeIdentifier) -eq "win") {
        $name = "githealth.exe"
    }

    $path = Join-Path (Get-PublishDirectory -RuntimeIdentifier $RuntimeIdentifier) $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "No $RuntimeIdentifier publication. Run build publish first."
    }

    return $path
}

function Invoke-Check {
    $report = Get-PrerequisiteReport
    $report | Format-Table -Property Tool, Expected, Found, Status -AutoSize | Out-Host

    # PowerShell unwraps an empty array returned by a function: without @(), $missing
    # would be $null and strict mode would refuse to read its element count.
    $missing = @(Get-MissingRequiredTools -Report $report)
    if ($missing.Count -gt 0) {
        throw "Required tools missing: $(($missing | ForEach-Object { $_.Tool }) -join ', ')."
    }

    $target = Get-HostRuntimeIdentifier
    Write-Host "Native target of this machine: $target"
    try {
        Assert-InstallerSupported -RuntimeIdentifier $target
        Write-Host "Velopack installer: can be built here."
    }
    catch {
        Write-Host "Velopack installer: unavailable here. $($_.Exception.Message)"
    }
}

<#
.SYNOPSIS
    Reads the API port where the front end declares it, so it is never invented twice.
#>
function Get-FrontendProxyPort {
    $path = Join-Path (Join-Path (Get-RepositoryRoot) $FrontendProjectPath) "proxy.conf.json"
    $proxy = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    return ([uri]$proxy."/api".target).Port
}

<#
.SYNOPSIS
    Starts "ng serve" without going through npm.

.DESCRIPTION
    The Angular CLI entry point is called directly: npm would interpose an extra
    process that stopping the script could no longer reach.
#>
function Start-FrontendDevServer {
    param([Parameter(Mandatory)][string]$FrontendRoot)

    $cli = Join-Path $FrontendRoot $AngularCliEntryPoint
    if (-not (Test-Path -LiteralPath $cli -PathType Leaf)) {
        throw "Missing dependencies. Run: npm ci --prefix $FrontendProjectPath"
    }

    return Start-Process -FilePath "node" -ArgumentList @($cli, "serve") `
        -WorkingDirectory $FrontendRoot -PassThru -NoNewWindow
}

function Stop-FrontendDevServer {
    param([System.Diagnostics.Process]$Process)

    if ($null -eq $Process -or $Process.HasExited) {
        return
    }

    Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
}

<#
.DESCRIPTION
    Neither flag is decorative. Without --port, the native launcher takes a random
    free port and the Angular proxy no longer finds the API; without --no-browser, it
    opens a Photino window on an empty wwwroot that only "publish" fills.
#>
function Invoke-Dev {
    $root = Get-RepositoryRoot
    $server = Start-FrontendDevServer -FrontendRoot (Join-Path $root $FrontendProjectPath)
    try {
        $port = Get-FrontendProxyPort
        Write-Host "API on http://localhost:$port — the interface is announced below by Angular."
        & dotnet run --project (Join-Path $root $ApiProjectPath) -- `
            --no-browser --port $port
    }
    finally {
        Stop-FrontendDevServer -Process $server
    }
}

function Write-CrossBuildWarning {
    param([Parameter(Mandatory)][string]$RuntimeIdentifier)

    if ((Get-RuntimeOperatingSystem $RuntimeIdentifier) -eq (Get-HostOperatingSystem)) {
        return
    }

    Write-Warning @"
Cross publication to $RuntimeIdentifier checks the compilation, it does not produce
a distributable artefact. From Windows, the archive loses the execute bit of Unix
binaries, and no smoke test can run here.
For a publishable artefact, go through .github/workflows/release.yml.
"@
}

function Invoke-Publish {
    param([Parameter(Mandatory)][string]$RuntimeIdentifier)

    Write-CrossBuildWarning -RuntimeIdentifier $RuntimeIdentifier
    & (Join-Path $PSScriptRoot "Publish-Native.ps1") -RuntimeIdentifier $RuntimeIdentifier
}

function Invoke-Run {
    param([Parameter(Mandatory)][string]$RuntimeIdentifier)

    if ((Get-RuntimeOperatingSystem $RuntimeIdentifier) -ne (Get-HostOperatingSystem)) {
        throw "A $RuntimeIdentifier publication does not run on this system."
    }

    # The executable is windowed. The call operator would hand back control at once,
    # without waiting for the application to close or picking up its exit code.
    $process = @{
        FilePath = Get-PublishedExecutable -RuntimeIdentifier $RuntimeIdentifier
        Wait = $true
        NoNewWindow = $true
        PassThru = $true
    }
    if ($ApplicationArguments.Count -gt 0) {
        $process.ArgumentList = $ApplicationArguments
    }

    exit (Start-Process @process).ExitCode
}

function Invoke-Installer {
    param([Parameter(Mandatory)][string]$RuntimeIdentifier)

    Assert-InstallerSupported -RuntimeIdentifier $RuntimeIdentifier
    Get-PublishedExecutable -RuntimeIdentifier $RuntimeIdentifier | Out-Null

    $packageVersion = $Version
    if ([string]::IsNullOrWhiteSpace($packageVersion)) {
        $packageVersion = Get-RepositoryVersion
    }

    & (Join-Path $PSScriptRoot "New-VelopackRelease.ps1") `
        -PublishDirectory (Get-PublishDirectory -RuntimeIdentifier $RuntimeIdentifier) `
        -Version $packageVersion `
        -RuntimeIdentifier $RuntimeIdentifier
}

function Invoke-Level {
    if ($Level -eq "check") {
        Invoke-Check
        return
    }

    if ($Level -eq "dev") {
        Invoke-Dev
        return
    }

    $target = Resolve-TargetRuntime
    switch ($Level) {
        "publish" { Invoke-Publish -RuntimeIdentifier $target }
        "run" { Invoke-Run -RuntimeIdentifier $target }
        "installer" { Invoke-Installer -RuntimeIdentifier $target }
    }
}

# These scripts address someone building the application, not someone debugging
# PowerShell: an expected refusal comes out as a sentence, with no exception trace.
try {
    Invoke-Level
}
catch {
    Write-Host ""
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
