[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../.."))
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-AvailableLoopbackPort {
    $listener = [System.Net.Sockets.TcpListener]::new(
        [System.Net.IPAddress]::Loopback,
        0
    )
    $listener.Start()
    try {
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Wait-ForContainerHealth {
    param([uri]$HealthUri)

    $deadline = [DateTimeOffset]::UtcNow.AddMinutes(2)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $HealthUri -TimeoutSec 3 -UseBasicParsing
            if ($response.StatusCode -eq 200) {
                return
            }
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }

    throw "Le conteneur GitHealth n'est pas devenu sain."
}

$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$projectName = "githealthsmoke$([Guid]::NewGuid().ToString('N').Substring(0, 10))"
$port = Get-AvailableLoopbackPort
$previousRepositoryRoot = $env:GITHEALTH_REPOSITORIES_ROOT
$previousPort = $env:GITHEALTH_HTTP_PORT
$env:GITHEALTH_REPOSITORIES_ROOT = $resolvedRoot
$env:GITHEALTH_HTTP_PORT = $port.ToString()

Push-Location $resolvedRoot
$failure = $null
$diagnostics = $null
$cleanupFailure = $null
try {
    & docker compose --project-name $projectName up --build --detach
    if ($LASTEXITCODE -ne 0) {
        throw "La construction ou le démarrage Docker a échoué."
    }

    $baseAddress = "http://127.0.0.1:$port"
    $baseUri = [uri]$baseAddress
    Wait-ForContainerHealth -HealthUri ([uri]"$baseAddress/health")
    $index = Invoke-WebRequest -Uri $baseUri -TimeoutSec 5 -UseBasicParsing
    if ($index.Content -notmatch "<app-root") {
        throw "Le conteneur ne sert pas l'interface Angular."
    }

    $userId = & docker compose --project-name $projectName exec -T githealth id -u
    if ($LASTEXITCODE -ne 0 -or [int]$userId -eq 0) {
        throw "Le conteneur GitHealth doit utiliser un UID non privilégié."
    }

    & docker compose --project-name $projectName exec -T githealth git --version | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Git n'est pas installé dans l'image."
    }

    & docker compose --project-name $projectName exec -T githealth `
        git -C /repositories rev-parse --is-inside-work-tree | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Le dépôt monté n'est pas accessible dans le conteneur."
    }

    & docker compose --project-name $projectName exec -T githealth `
        sh -c "test ! -w /repositories && echo smoke > /data/smoke-marker"
    if ($LASTEXITCODE -ne 0) {
        throw "Les permissions des montages Docker sont invalides."
    }

    & docker compose --project-name $projectName up --detach --force-recreate
    if ($LASTEXITCODE -ne 0) {
        throw "La recréation du conteneur a échoué."
    }

    Wait-ForContainerHealth -HealthUri ([uri]"$baseAddress/health")
    & docker compose --project-name $projectName exec -T githealth `
        test -f /data/smoke-marker
    if ($LASTEXITCODE -ne 0) {
        throw "Le volume de données n'a pas survécu à la recréation."
    }
}
catch {
    $failure = $_
    try {
        $diagnostics = @(
            (& docker compose --project-name $projectName ps --all 2>&1),
            (& docker compose --project-name $projectName logs --no-color 2>&1)
        ) -join "`n"
    }
    catch {
        $diagnostics = "Diagnostics Docker indisponibles : $_"
    }
}
finally {
    try {
        & docker compose --project-name $projectName down --volumes --remove-orphans
        if ($LASTEXITCODE -ne 0) {
            $cleanupFailure = "Docker Compose n'a pas pu nettoyer le projet $projectName."
        }
    }
    catch {
        $cleanupFailure = $_
    }
    finally {
        $env:GITHEALTH_REPOSITORIES_ROOT = $previousRepositoryRoot
        $env:GITHEALTH_HTTP_PORT = $previousPort
        Pop-Location
    }
}

if ($null -ne $failure -or $null -ne $cleanupFailure) {
    throw "$failure`nNettoyage : $cleanupFailure`nDiagnostics :`n$diagnostics"
}

Write-Output "Smoke test Docker réussi sur le port $port."
