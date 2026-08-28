$ErrorActionPreference = "Stop"

function Assert-Equal {
    param($Expected, $Actual, [string]$Message)

    if ($Expected -ne $Actual) {
        throw "$Message Attendu : '$Expected'. Reçu : '$Actual'."
    }
}

function Assert-True {
    param([bool]$Condition, [string]$Message)

    if (-not $Condition) {
        throw $Message
    }
}

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$dockerConfigPath = Join-Path ([System.IO.Path]::GetTempPath()) "githealth-docker-config"
$previousDockerConfig = $env:DOCKER_CONFIG
$previousHttpPort = $env:GITHEALTH_HTTP_PORT
[System.IO.Directory]::CreateDirectory($dockerConfigPath) | Out-Null
$env:DOCKER_CONFIG = $dockerConfigPath
$env:GITHEALTH_HTTP_PORT = "8080"

Push-Location $repositoryRoot
try {
    $composeOutput = & docker compose --file compose.yaml config --format json 2>$null
    Assert-Equal 0 $LASTEXITCODE "La configuration Compose doit être valide."

    $configuration = ($composeOutput -join "`n") | ConvertFrom-Json
    $serviceProperties = @($configuration.services.PSObject.Properties)
    Assert-Equal 1 $serviceProperties.Count "Compose doit définir un seul service."

    $service = $configuration.services.githealth
    Assert-True ($null -ne $service) "Le service githealth doit exister."
    Assert-True $service.read_only "Le système de fichiers du conteneur doit être en lecture seule."

    $ports = @($service.ports)
    Assert-Equal 1 $ports.Count "Un seul port doit être publié."
    Assert-Equal "127.0.0.1" $ports[0].host_ip "Le port doit rester sur loopback."
    Assert-Equal 8080 $ports[0].target "Le conteneur doit écouter sur le port 8080."

    $mounts = @($service.volumes)
    $dataMount = $mounts | Where-Object target -eq "/data"
    $repositoriesMount = $mounts | Where-Object target -eq "/repositories"
    Assert-Equal "volume" $dataMount.type "/data doit utiliser un volume nommé."
    Assert-Equal "bind" $repositoriesMount.type "/repositories doit utiliser un bind mount."
    Assert-True $repositoriesMount.read_only "/repositories doit être en lecture seule."

    $dockerfile = Get-Content -LiteralPath "Dockerfile" -Raw
    $normalizedDockerfile = $dockerfile.Replace("'", "").Replace('"', "")
    Assert-True (
        $dockerfile.Contains('USER $APP_UID')
    ) "L'image doit utiliser un UID non privilégié."
    Assert-True (
        $normalizedDockerfile -notmatch 'safe\.directory\s+\*(\s|$)'
    ) "Git ne doit jamais autoriser safe.directory=* globalement."

    Write-Output "Configuration Docker Compose vérifiée."
}
finally {
    $env:DOCKER_CONFIG = $previousDockerConfig
    $env:GITHEALTH_HTTP_PORT = $previousHttpPort
    Pop-Location
}
