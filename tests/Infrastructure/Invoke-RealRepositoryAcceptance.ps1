[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateCount(2, 2)]
    [string[]]$RepositoryPath,
    [string]$PublishDirectory = "artifacts/e2e-app",
    [string]$ReportPath = "artifacts/acceptance/real-repositories.json"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "Acceptance/ProcessHelpers.ps1")
. (Join-Path $PSScriptRoot "Acceptance/RecipeHelpers.ps1")

function Get-AvailableLoopbackPort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try {
        return ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Wait-ForHealth {
    param([string]$BaseAddress, [Diagnostics.Process]$Process)

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
    while ([DateTimeOffset]::UtcNow -lt $deadline -and -not $Process.HasExited) {
        try {
            $response = Invoke-WebRequest "$BaseAddress/health" `
                -TimeoutSec 2 -UseBasicParsing
            if ($response.StatusCode -eq 200) {
                return
            }
        }
        catch {
            Start-Sleep -Milliseconds 200
        }
    }

    throw "GitHealth n'est pas devenu sain sur $BaseAddress."
}

function New-LocalSession {
    param([string]$BaseAddress)

    $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $headers = @{ Accept = "text/html"; "Sec-Fetch-Mode" = "navigate" }
    Invoke-WebRequest $BaseAddress -WebSession $session -Headers $headers `
        -UseBasicParsing | Out-Null
    return $session
}

function Get-SecurityHeaders {
    param(
        [string]$BaseAddress,
        [Microsoft.PowerShell.Commands.WebRequestSession]$Session
    )

    $uri = [uri]$BaseAddress
    $token = $Session.Cookies.GetCookies($uri) |
        Where-Object Name -EQ "XSRF-TOKEN" |
        Select-Object -ExpandProperty Value -First 1
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Le jeton anti-forgery local est absent."
    }

    return @{
        Origin = $BaseAddress
        "Sec-Fetch-Site" = "same-origin"
        "X-XSRF-TOKEN" = [uri]::UnescapeDataString($token)
    }
}

function Invoke-ApiMutation {
    param([hashtable]$Context, [hashtable]$Request)

    $parameters = @{
        Uri = "$($Context.BaseAddress)$($Request.Path)"
        Method = $Request.Method
        WebSession = $Context.Session
        Headers = Get-SecurityHeaders $Context.BaseAddress $Context.Session
        TimeoutSec = 30
    }
    if ($Request.ContainsKey("Body") -and $null -ne $Request.Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = ConvertTo-Json $Request.Body -Depth 8 -Compress
    }

    return Invoke-RestMethod @parameters
}

function Invoke-GitRead {
    param([string]$Repository, [string[]]$Arguments, [switch]$AllowFailure)

    $configuration = @(
        "--no-pager", "-c", "core.fsmonitor=false",
        "-c", "maintenance.auto=false", "-c", "gc.auto=0",
        "-c", "credential.helper=", "-c", "protocol.allow=never"
    )
    $environment = Push-ReadOnlyGitEnvironment
    try {
        $output = & git @configuration -C $Repository @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-ReadOnlyGitEnvironment $environment
    }
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "La lecture Git a échoué avec le code $exitCode."
    }
    return @($output)
}

function Get-RepositoryConfiguration {
    param([string]$Repository)
    $references = Invoke-GitRead $Repository @(
        "for-each-ref", "--format=%(refname)", "refs/heads", "refs/remotes"
    )
    $head = @(Invoke-GitRead $Repository @("symbolic-ref", "HEAD"))[0]
    $reference = @(
        "refs/heads/main", "refs/heads/master", $head,
        "refs/remotes/origin/main", "refs/remotes/origin/master"
    ) | Where-Object { $references -contains $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($reference)) {
        throw "Aucune référence de comparaison exploitable."
    }
    $localCount = @($references | Where-Object { $_ -like "refs/heads/*" }).Count
    $remoteCount = @($references | Where-Object {
        $_ -like "refs/remotes/origin/*" -and $_ -notlike "*/HEAD"
    }).Count
    $namespace = if ($remoteCount -gt $localCount) {
        "refs/remotes/origin/*"
    } else {
        "refs/heads/*"
    }

    return [PSCustomObject]@{
        Reference = $reference
        Namespace = $namespace
        BranchCount = $references.Count
    }
}

function Get-IndexFingerprint {
    param([string]$Repository)

    $indexPath = @(Invoke-GitRead $Repository @(
        "rev-parse", "--path-format=absolute", "--git-path", "index"
    ))[0]
    if (Test-Path -LiteralPath $indexPath -PathType Leaf) {
        return (Get-FileHash -LiteralPath $indexPath -Algorithm SHA256).Hash
    }

    return "no-index"
}

function Get-RepositoryFingerprint {
    param([string]$Repository)

    $refs = Invoke-GitRead $Repository @(
        "for-each-ref", "--format=%(refname)%00%(objectname)"
    )
    $status = Invoke-GitRead $Repository @("status", "--porcelain=v2", "--branch")
    $reflogs = Invoke-GitRead $Repository @(
        "reflog", "show", "--all", "--format=%H%x00%gD"
    ) -AllowFailure
    $worktreeDiff = Invoke-GitRead $Repository @(
        "diff", "--binary", "--no-ext-diff", "--no-textconv", "--"
    )
    $content = @($refs; $status; $reflogs; $worktreeDiff; (Get-IndexFingerprint $Repository))
    $bytes = [Text.Encoding]::UTF8.GetBytes(($content -join "`n"))
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash($bytes)
        return [BitConverter]::ToString($hash).Replace("-", "")
    }
    finally {
        $sha256.Dispose()
    }
}

function Wait-ForAnalysis {
    param([string]$BaseAddress, [object]$Session, [string]$AnalysisId)

    $deadline = [DateTimeOffset]::UtcNow.AddMinutes(10)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $status = Invoke-RestMethod "$BaseAddress/api/analyses/$AnalysisId" `
            -WebSession $Session -TimeoutSec 10
        if ($status.status -eq "Completed") {
            return
        }
        if ($status.status -in @("Failed", "Cancelled")) {
            throw "Analyse réelle interrompue : $($status.failureCode)."
        }
        Start-Sleep -Milliseconds 250
    }

    throw "L'analyse réelle a dépassé dix minutes."
}

function Remove-AcceptanceDirectory {
    param([string]$Path)

    $resolved = [IO.Path]::GetFullPath($Path)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) `
        -or [IO.Path]::GetFileName($resolved) -notlike "githealth-acceptance-*") {
        throw "Le répertoire temporaire de recette est invalide."
    }
    if (Test-Path -LiteralPath $resolved) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

$resolvedPublish = (Resolve-Path -LiteralPath $PublishDirectory).Path
$repositories = $RepositoryPath | ForEach-Object {
    (Resolve-Path -LiteralPath $_).Path
}
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) `
    "githealth-acceptance-$([Guid]::NewGuid().ToString('N'))"
$dataPath = Join-Path $temporaryRoot "data"
$outputPath = Join-Path $temporaryRoot "exports"
[IO.Directory]::CreateDirectory($dataPath) | Out-Null
[IO.Directory]::CreateDirectory($outputPath) | Out-Null

$port = Get-AvailableLoopbackPort
$baseAddress = "http://127.0.0.1:$port"
$process = $null
$recipes = @()
try {
    $process = Start-GitHealth $resolvedPublish $dataPath $port
    Wait-ForHealth $baseAddress $process
    $session = New-LocalSession $baseAddress
    $context = @{
        BaseAddress = $baseAddress
        Session = $session
        OutputPath = $outputPath
    }
    for ($index = 0; $index -lt $repositories.Count; $index++) {
        $recipes += Invoke-RepositoryRecipe $context $repositories[$index] ($index + 1)
    }
    Assert-RealRepositoryCoverage $recipes

    Invoke-WebRequest "$baseAddress/api/exports/database" `
        -WebSession $session -OutFile (Join-Path $outputPath "githealth.db") `
        -UseBasicParsing
    Stop-GitHealth $process
    $process = $null
    $process = Start-GitHealth $resolvedPublish $dataPath $port
    Wait-ForHealth $baseAddress $process
    $session = New-LocalSession $baseAddress
    $context.Session = $session
    Assert-RestartedSnapshots $context $recipes

    for ($index = 0; $index -lt $repositories.Count; $index++) {
        $after = Get-RepositoryFingerprint $repositories[$index]
        $unchanged = $after -eq $recipes[$index].Before
        $recipes[$index].Evidence.repositoryUnchanged = $unchanged
        if (-not $unchanged) {
            throw "Le dépôt réel $($index + 1) a changé pendant la recette."
        }
    }

    $report = [ordered]@{
        version = "0.1.0"
        executedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
        platform = [Runtime.InteropServices.RuntimeInformation]::OSDescription
        restartPreservedProjects = $true
        restartPreservedSnapshots = $true
        csvAndDatabaseExports = $true
        repositories = @($recipes | ForEach-Object Evidence)
    }
    $resolvedReport = [IO.Path]::GetFullPath($ReportPath)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($resolvedReport)) |
        Out-Null
    $reportJson = ConvertTo-Json $report -Depth 8
    [IO.File]::WriteAllText(
        $resolvedReport,
        $reportJson,
        [Text.UTF8Encoding]::new($false))
    Write-Output "Recette réelle validée : $resolvedReport"
}
finally {
    if ($null -ne $process) {
        Stop-GitHealth $process
    }
    Remove-AcceptanceDirectory $temporaryRoot
}
