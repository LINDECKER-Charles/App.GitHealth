[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PublishDirectory,
    [string]$RepositoryPath = (Resolve-Path (Join-Path $PSScriptRoot "../.."))
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

function Wait-ForHealth {
    param([uri]$HealthUri, [System.Diagnostics.Process]$Process)

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if ($Process.HasExited) {
            throw "GitHealth stopped with exit code $($Process.ExitCode)."
        }

        try {
            $response = Invoke-WebRequest -Uri $HealthUri -TimeoutSec 2 -UseBasicParsing
            if ($response.StatusCode -eq 200) {
                return
            }
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }

    throw "GitHealth did not respond on $HealthUri within the allotted time."
}

function Set-ProcessArguments {
    param(
        [System.Diagnostics.ProcessStartInfo]$StartInfo,
        [string[]]$Arguments
    )

    $argumentListProperty = $StartInfo.PSObject.Properties["ArgumentList"]
    if ($null -ne $argumentListProperty) {
        foreach ($argument in $Arguments) {
            $StartInfo.ArgumentList.Add($argument)
        }
        return
    }

    $StartInfo.Arguments = ($Arguments | ForEach-Object {
        ConvertTo-CommandLineArgument $_
    }) -join " "
}

function ConvertTo-CommandLineArgument {
    param([AllowEmptyString()][string]$Argument)

    if ($Argument.Length -gt 0 -and $Argument -notmatch '[\s"]') {
        return $Argument
    }

    $escaped = [regex]::Replace($Argument, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function New-RedirectedStartInfo {
    param([string]$Executable, [string[]]$Arguments)

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    $startInfo.StandardOutputEncoding = $utf8
    $startInfo.StandardErrorEncoding = $utf8
    Set-ProcessArguments -StartInfo $startInfo -Arguments $Arguments
    return $startInfo
}

function Stop-ProcessTree {
    param([System.Diagnostics.Process]$Process)

    $killTreeMethod = [System.Diagnostics.Process].GetMethod(
        "Kill",
        [type[]]@([bool])
    )
    if ($null -ne $killTreeMethod) {
        [void]$killTreeMethod.Invoke($Process, @($true))
        return
    }

    $Process.Kill()
}

function Invoke-ExpectedFailure {
    param(
        [string]$Executable,
        [string[]]$Arguments,
        [string]$ExpectedMessage
    )

    $failureInfo = New-RedirectedStartInfo -Executable $Executable -Arguments $Arguments

    $failedProcess = [System.Diagnostics.Process]::new()
    $processStarted = $false
    try {
        $failedProcess.StartInfo = $failureInfo
        $processStarted = $failedProcess.Start()
        if (-not $processStarted) {
            throw "The diagnostic process did not start."
        }

        $outputTask = $failedProcess.StandardOutput.ReadToEndAsync()
        $errorTask = $failedProcess.StandardError.ReadToEndAsync()
        if (-not $failedProcess.WaitForExit(15000)) {
            Stop-ProcessTree -Process $failedProcess
            $failedProcess.WaitForExit()
            throw "The diagnostic process did not stop."
        }

        $output = $outputTask.GetAwaiter().GetResult()
        $diagnostic = $errorTask.GetAwaiter().GetResult()
        $combinedOutput = "$diagnostic`n$output"
        if ($failedProcess.ExitCode -ne 1 -or $combinedOutput -notlike "*$ExpectedMessage*") {
            throw "Unexpected diagnostic: $combinedOutput"
        }
    }
    finally {
        if ($processStarted -and -not $failedProcess.HasExited) {
            Stop-ProcessTree -Process $failedProcess
            $failedProcess.WaitForExit()
        }

        $failedProcess.Dispose()
    }
}

$resolvedPublish = (Resolve-Path -LiteralPath $PublishDirectory).Path
$resolvedRepository = (Resolve-Path -LiteralPath $RepositoryPath).Path
$isWindowsPlatform = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows
)
$executableName = if ($isWindowsPlatform) { "githealth.exe" } else { "githealth" }
$executable = Join-Path $resolvedPublish $executableName
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Native executable not found: $executable"
}

$smokeRoot = Join-Path (
    [System.IO.Path]::GetTempPath()
) "githealth-native-smoke-$([Guid]::NewGuid().ToString('N'))"
[System.IO.Directory]::CreateDirectory($smokeRoot) | Out-Null
$port = Get-AvailableLoopbackPort
$baseAddress = "http://127.0.0.1:$port"
$baseUri = [uri]$baseAddress
$startInfo = New-RedirectedStartInfo -Executable $executable -Arguments @(
    "--no-browser",
    "--port", $port.ToString(),
    "--data-dir", $smokeRoot,
    "--repo", $resolvedRepository
)

$process = [System.Diagnostics.Process]::new()
$process.StartInfo = $startInfo
$failure = $null
$cleanupFailure = $null
$standardOutput = $null
$standardError = $null
$outputTask = $null
$errorTask = $null
$processStarted = $false
try {
    $processStarted = $process.Start()
    if (-not $processStarted) {
        throw "The GitHealth process did not start."
    }

    $outputTask = $process.StandardOutput.ReadToEndAsync()
    $errorTask = $process.StandardError.ReadToEndAsync()
    Wait-ForHealth -HealthUri ([uri]"$baseAddress/health") -Process $process

    $index = Invoke-WebRequest -Uri $baseUri -TimeoutSec 5 -UseBasicParsing
    if ($index.Content -notmatch "<app-root") {
        throw "The Angular bundle is not served by the native executable."
    }

    $runtime = Invoke-RestMethod -Uri ([uri]"$baseAddress/api/runtime") -TimeoutSec 5
    if ($runtime.mode -ne "native" -or $runtime.initialRepositoryPath -ne $resolvedRepository) {
        throw "The runtime diagnostic does not reflect the launcher options."
    }

    if (-not (Test-Path -LiteralPath (Join-Path $smokeRoot "githealth.db"))) {
        throw "The SQLite database was not created in --data-dir."
    }

    Invoke-ExpectedFailure -Executable $executable -Arguments @(
        "--no-browser", "--port", (Get-AvailableLoopbackPort).ToString(),
        "--data-dir", $smokeRoot
    ) -ExpectedMessage "another GitHealth instance"
    Invoke-ExpectedFailure -Executable $executable -Arguments @(
        "--no-browser", "--port", $port.ToString(),
        "--data-dir", (Join-Path $smokeRoot "port-conflict")
    ) -ExpectedMessage "Loopback port $port"
    $invalidDataDirectory = Join-Path $smokeRoot "invalid-data-directory"
    [System.IO.File]::WriteAllText($invalidDataDirectory, "file")
    Invoke-ExpectedFailure -Executable $executable -Arguments @(
        "--no-browser", "--port", (Get-AvailableLoopbackPort).ToString(),
        "--data-dir", $invalidDataDirectory
    ) -ExpectedMessage "unreachable or not writable"
}
catch {
    $failure = $_
}
finally {
    try {
        if ($processStarted -and -not $process.HasExited) {
            Stop-ProcessTree -Process $process
            $process.WaitForExit()
        }

        if ($null -ne $outputTask) {
            $standardOutput = $outputTask.GetAwaiter().GetResult()
            $standardError = $errorTask.GetAwaiter().GetResult()
        }

        $resolvedSmokeRoot = [System.IO.Path]::GetFullPath($smokeRoot)
        $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if (-not $resolvedSmokeRoot.StartsWith(
            $temporaryRoot,
            [StringComparison]::OrdinalIgnoreCase
        ) -or -not ([System.IO.Path]::GetFileName($resolvedSmokeRoot)).StartsWith(
            "githealth-native-smoke-",
            [StringComparison]::Ordinal
        )) {
            throw "The temporary directory to clean up is outside the allowed scope."
        }

        Remove-Item -LiteralPath $resolvedSmokeRoot -Recurse -Force
    }
    catch {
        $cleanupFailure = $_
    }
    finally {
        $process.Dispose()
    }
}

if ($null -ne $failure -or $null -ne $cleanupFailure) {
    throw (
        "$failure`nCleanup: $cleanupFailure" +
        "`nOutput:`n$standardOutput" +
        "`nErrors:`n$standardError"
    )
}

Write-Output "Native smoke test passed on $baseUri."
