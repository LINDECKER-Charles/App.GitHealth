<#
.SYNOPSIS
    Point d'entrée unique des builds locaux de GitHealth, sur Windows, macOS et Linux.

.DESCRIPTION
    Un niveau par intention, du plus court au plus complet :

      check      constate l'outillage du poste et la cible qu'il sait produire
      dev        API et interface Angular en direct, rechargement compris
      publish    exécutable autonome, tel qu'il est distribué
      run        lance le résultat de « publish »
      installer  installeur Velopack et flux de mise à jour

    Le script n'implémente aucune étape de publication : il délègue à
    eng/Publish-Native.ps1 et eng/New-VelopackRelease.ps1, que la CI appelle
    aussi. Un build local et un build de release suivent donc le même chemin.

    Compatible Windows PowerShell 5.1 et PowerShell 7 ; voir eng/README.md.

.EXAMPLE
    ./eng/build.sh check

.EXAMPLE
    ./eng/build.sh publish
    ./eng/build.sh run --repo ~/Dev/MonDepot

.EXAMPLE
    eng\build.cmd installer
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("check", "dev", "publish", "run", "installer")]
    [string]$Level = "check",

    # Vide : la cible de la machine hôte.
    [ValidateSet("win-x64", "osx-x64", "osx-arm64", "linux-x64")]
    [string]$Runtime,

    # Vide : la version portée par Directory.Build.props.
    [string]$Version,

    # Tout ce qui n'est pas reconnu ci-dessus part au niveau « run », vers
    # l'application. PowerShell -File ne connaît pas le séparateur « -- » : ne pas
    # l'employer, les arguments suivent le niveau directement.
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
        throw "Aucune publication $RuntimeIdentifier. Lancer d'abord : build publish."
    }

    return $path
}

function Invoke-Check {
    $report = Get-PrerequisiteReport
    $report | Format-Table -Property Outil, Attendu, Trouvé, Statut -AutoSize | Out-Host

    # PowerShell déballe un tableau vide renvoyé par une fonction : sans @(), $missing
    # vaudrait $null et le mode strict refuserait d'en lire le nombre d'éléments.
    $missing = @(Get-MissingRequiredTools -Report $report)
    if ($missing.Count -gt 0) {
        throw "Outils requis absents : $(($missing | ForEach-Object { $_.Outil }) -join ', ')."
    }

    $target = Get-HostRuntimeIdentifier
    Write-Host "Cible native de ce poste : $target"
    try {
        Assert-InstallerSupported -RuntimeIdentifier $target
        Write-Host "Installeur Velopack : constructible ici."
    }
    catch {
        Write-Host "Installeur Velopack : indisponible ici. $($_.Exception.Message)"
    }
}

<#
.SYNOPSIS
    Lit le port de l'API là où le front le déclare, pour ne pas l'inventer une seconde fois.
#>
function Get-FrontendProxyPort {
    $path = Join-Path (Join-Path (Get-RepositoryRoot) $FrontendProjectPath) "proxy.conf.json"
    $proxy = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    return ([uri]$proxy."/api".target).Port
}

<#
.SYNOPSIS
    Démarre « ng serve » sans passer par npm.

.DESCRIPTION
    L'entrée de la CLI Angular est appelée directement : npm interposerait un
    processus supplémentaire, que l'arrêt du script ne saurait plus atteindre.
#>
function Start-FrontendDevServer {
    param([Parameter(Mandatory)][string]$FrontendRoot)

    $cli = Join-Path $FrontendRoot $AngularCliEntryPoint
    if (-not (Test-Path -LiteralPath $cli -PathType Leaf)) {
        throw "Dépendances absentes. Lancer : npm ci --prefix $FrontendProjectPath"
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
    Les deux drapeaux ne sont pas décoratifs. Sans --port, le lanceur natif prend un
    port libre au hasard et le proxy Angular ne trouve plus l'API ; sans --no-browser,
    il ouvre une fenêtre Photino sur un wwwroot vide, que seul « publish » remplit.
#>
function Invoke-Dev {
    $root = Get-RepositoryRoot
    $server = Start-FrontendDevServer -FrontendRoot (Join-Path $root $FrontendProjectPath)
    try {
        $port = Get-FrontendProxyPort
        Write-Host "API sur http://localhost:$port — interface annoncée ci-dessous par Angular."
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
Publication croisée vers $RuntimeIdentifier : elle vérifie la compilation, elle ne
produit pas un artefact distribuable. Depuis Windows, l'archive perd le bit
d'exécution des binaires Unix, et aucun smoke test ne peut s'exécuter ici.
Pour un artefact publiable, passer par .github/workflows/release.yml.
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
        throw "Une publication $RuntimeIdentifier ne s'exécute pas sur ce système."
    }

    # L'exécutable est fenêtré. L'opérateur d'appel rendrait la main aussitôt, sans
    # attendre la fermeture de l'application ni relever son code de sortie.
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

# Ces scripts s'adressent à quelqu'un qui construit l'application, pas à quelqu'un qui
# débogue PowerShell : un refus attendu sort comme une phrase, sans trace d'exception.
try {
    Invoke-Level
}
catch {
    Write-Host ""
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
