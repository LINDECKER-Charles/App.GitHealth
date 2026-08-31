$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Assert-Equal {
    param($Expected, $Actual, [string]$Message)

    if ($Expected -ne $Actual) {
        throw "$Message Expected: '$Expected'. Got: '$Actual'."
    }
}

function Assert-True {
    param([bool]$Condition, [string]$Message)

    if (-not $Condition) {
        throw $Message
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$dockerConfigPath = Join-Path (
    [System.IO.Path]::GetTempPath()
) "githealth-docker-config-$([Guid]::NewGuid().ToString('N'))"
$previousDockerConfig = $env:DOCKER_CONFIG
$previousHttpPort = $env:GITHEALTH_HTTP_PORT
$previousRepositoryRoot = $env:GITHEALTH_REPOSITORIES_ROOT
[System.IO.Directory]::CreateDirectory($dockerConfigPath) | Out-Null
$env:DOCKER_CONFIG = $dockerConfigPath
$env:GITHEALTH_HTTP_PORT = "8080"
$env:GITHEALTH_REPOSITORIES_ROOT = $repositoryRoot

Push-Location $repositoryRoot
try {
    $composeOutput = & docker compose --file compose.yaml config --format json 2>$null
    Assert-Equal 0 $LASTEXITCODE "The Compose configuration must be valid."

    $configuration = ($composeOutput -join "`n") | ConvertFrom-Json
    $serviceProperties = @($configuration.services.PSObject.Properties)
    Assert-Equal 1 $serviceProperties.Count "Compose must define a single service."

    $service = $configuration.services.githealth
    Assert-True ($null -ne $service) "The githealth service must exist."
    Assert-True (
        $service.read_only
    ) "The container file system must be read only."
    Assert-Equal 128 $service.pids_limit "The process count must be capped."
    Assert-True (
        @($service.cap_drop) -contains "ALL"
    ) "All Linux capabilities must be dropped."

    $ports = @($service.ports)
    Assert-Equal 1 $ports.Count "A single port must be published."
    Assert-Equal "127.0.0.1" $ports[0].host_ip "The port must stay on loopback."
    Assert-Equal 8080 $ports[0].target "The container must listen on port 8080."

    $mounts = @($service.volumes)
    $dataMount = @($mounts | Where-Object target -eq "/data")
    $repositoriesMount = @($mounts | Where-Object target -eq "/repositories")
    Assert-Equal 1 $dataMount.Count "/data must have a single mount."
    Assert-Equal 1 $repositoriesMount.Count "/repositories must have a single mount."
    $dataMount = $dataMount[0]
    $repositoriesMount = $repositoriesMount[0]
    Assert-Equal "volume" $dataMount.type "/data must use a named volume."
    Assert-Equal "bind" $repositoriesMount.type "/repositories must use a bind mount."
    Assert-True $repositoriesMount.read_only "/repositories must be read only."

    $dockerfile = Get-Content -LiteralPath "Dockerfile" -Raw
    $normalizedDockerfile = $dockerfile.Replace("'", "").Replace('"', "")
    Assert-True (
        $dockerfile.Contains('USER $APP_UID')
    ) "The image must use an unprivileged UID."
    Assert-True (
        $normalizedDockerfile -notmatch 'safe\.directory\s+\*(\s|$)'
    ) "Git must never allow safe.directory=* globally."
    Assert-True (
        $dockerfile.Contains("http://127.0.0.1:8080/health")
    ) "The Docker healthcheck must explicitly target the loopback."
    Assert-True (
        $dockerfile.Contains("STOPSIGNAL SIGTERM")
    ) "The image must declare the graceful shutdown signal."
    Assert-True (
        $dockerfile.Contains('ENTRYPOINT ["dotnet", "githealth.dll"]')
    ) "The image must start the assembly produced by the GitHealth launcher."
    Assert-True (
        $dockerfile.Contains("GitHealth__DataDirectory=/data")
    ) "The image must use /data even when it starts outside Compose."

    # nvm and actions/setup-node also accept the "v24.20.0" form: the comparison uses
    # the number alone. The pattern tolerates FROM flags (--platform in particular),
    # without which a node stage would escape it and never be checked against .nvmrc.
    $pinnedNodeVersion = (Get-Content -LiteralPath ".nvmrc" -Raw).Trim() -replace '^[vV]', ''
    $nodeImages = @([regex]::Matches(
        $dockerfile,
        '(?im)^FROM\s+(?:--\S+\s+)*node:(?<tag>\S+)'))
    Assert-True (
        $nodeImages.Count -gt 0
    ) "The Dockerfile must build the front end from an official tagged node image."
    foreach ($nodeImage in $nodeImages) {
        $imageVersion = ($nodeImage.Groups["tag"].Value -split "-", 2)[0]
        Assert-Equal $pinnedNodeVersion $imageVersion (
            "The Dockerfile's Node image must stay aligned with .nvmrc."
        )
    }

    Write-Output "Docker Compose configuration verified."
}
finally {
    $env:DOCKER_CONFIG = $previousDockerConfig
    $env:GITHEALTH_HTTP_PORT = $previousHttpPort
    $env:GITHEALTH_REPOSITORIES_ROOT = $previousRepositoryRoot
    Pop-Location
    $resolvedDockerConfig = [System.IO.Path]::GetFullPath($dockerConfigPath)
    $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    if ($resolvedDockerConfig.StartsWith(
        $temporaryRoot,
        [StringComparison]::OrdinalIgnoreCase
    )) {
        Remove-Item -LiteralPath $resolvedDockerConfig -Recurse -Force
    }
}
