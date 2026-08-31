[CmdletBinding()]
param(
    [ValidateSet("win-x64", "osx-x64", "osx-arm64", "linux-x64")]
    [string[]]$RuntimeIdentifier = @("win-x64", "osx-x64", "osx-arm64", "linux-x64"),
    [string]$OutputRoot = (Join-Path $PSScriptRoot "../artifacts/publish")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Reset-PublishDirectory {
    param([string]$DirectoryPath, [string]$RootPath)

    $resolvedRoot = [System.IO.Path]::GetFullPath($RootPath)
    $resolvedDirectory = [System.IO.Path]::GetFullPath($DirectoryPath)
    $rootPrefix = $resolvedRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    ) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedDirectory.StartsWith(
        $rootPrefix,
        [StringComparison]::OrdinalIgnoreCase
    )) {
        throw "The publication directory must stay under '$resolvedRoot'."
    }

    if ([System.IO.Directory]::Exists($resolvedDirectory)) {
        Remove-Item -LiteralPath $resolvedDirectory -Recurse -Force
    }

    [System.IO.Directory]::CreateDirectory($resolvedDirectory) | Out-Null
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $repositoryRoot "src/App.GitHealth.Api/App.GitHealth.Api.csproj"
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
[System.IO.Directory]::CreateDirectory($resolvedOutputRoot) | Out-Null

foreach ($runtime in $RuntimeIdentifier) {
    $destination = Join-Path $resolvedOutputRoot $runtime
    Reset-PublishDirectory -DirectoryPath $destination -RootPath $resolvedOutputRoot
    Write-Host "Self-contained publication of GitHealth for $runtime..."
    & dotnet publish $project `
        --configuration Release `
        --runtime $runtime `
        --self-contained true `
        --output $destination `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        -p:UseAppHost=true
    if ($LASTEXITCODE -ne 0) {
        throw "The $runtime publication failed."
    }

    $executable = if ($runtime -eq "win-x64") { "githealth.exe" } else { "githealth" }
    $requiredFiles = @(
        (Join-Path $destination $executable),
        (Join-Path (Join-Path $destination "wwwroot") "index.html")
    )
    foreach ($requiredFile in $requiredFiles) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "The $runtime publication does not contain '$requiredFile'."
        }
    }

    if ($runtime -eq "win-x64") {
        $archive = Join-Path $resolvedOutputRoot "githealth-$runtime.zip"
        Compress-Archive -Path (Join-Path $destination "*") -DestinationPath $archive -Force
    }
    else {
        $archive = Join-Path $resolvedOutputRoot "githealth-$runtime.tar.gz"
        & tar -czf $archive -C $destination .
        if ($LASTEXITCODE -ne 0) {
            throw "Archiving the $runtime publication failed."
        }
    }

    $archiveInfo = Get-Item -LiteralPath $archive
    if ($archiveInfo.Length -eq 0) {
        throw "The archive of the $runtime publication is empty."
    }

    Write-Host "Artefact ready: $destination"
    Write-Host "Archive ready: $archive"
}
