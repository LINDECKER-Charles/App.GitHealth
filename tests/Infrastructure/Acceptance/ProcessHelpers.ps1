function ConvertTo-AcceptanceProcessArgument {
    param([AllowEmptyString()][string]$Argument)

    if ($Argument.Length -gt 0 -and $Argument -notmatch '[\s"]') {
        return $Argument
    }

    $escaped = [regex]::Replace($Argument, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function Set-AcceptanceProcessArguments {
    param([Diagnostics.ProcessStartInfo]$StartInfo, [string[]]$Arguments)

    if ($null -ne $StartInfo.PSObject.Properties["ArgumentList"]) {
        foreach ($argument in $Arguments) {
            [void]$StartInfo.ArgumentList.Add($argument)
        }
        return
    }

    $StartInfo.Arguments = ($Arguments | ForEach-Object {
        ConvertTo-AcceptanceProcessArgument $_
    }) -join " "
}

function Test-UnsafeGitEnvironmentName {
    param([string]$Name)

    $exactNames = @(
        "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_ASKPASS", "GIT_COMMON_DIR",
        "GIT_CONFIG", "GIT_CONFIG_COUNT", "GIT_CONFIG_GLOBAL", "GIT_CONFIG_NOSYSTEM",
        "GIT_CONFIG_PARAMETERS", "GIT_CONFIG_SYSTEM", "GIT_DIR", "GIT_EXTERNAL_DIFF",
        "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_SSH", "GIT_SSH_COMMAND",
        "GIT_WORK_TREE", "SSH_ASKPASS"
    )
    return $Name -in $exactNames -or $Name.StartsWith(
        "GIT_TRACE", [StringComparison]::OrdinalIgnoreCase)
}

function Push-ReadOnlyGitEnvironment {
    $snapshot = @{}
    Get-ChildItem Env: | Where-Object {
        Test-UnsafeGitEnvironmentName $_.Name
    } | ForEach-Object {
        $snapshot[$_.Name] = $_.Value
        [Environment]::SetEnvironmentVariable($_.Name, $null, "Process")
    }

    $overrides = @{
        GIT_NO_LAZY_FETCH = "1"
        GIT_OPTIONAL_LOCKS = "0"
        GIT_TERMINAL_PROMPT = "0"
    }
    foreach ($name in $overrides.Keys) {
        if (-not $snapshot.ContainsKey($name)) {
            $snapshot[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
        }
        [Environment]::SetEnvironmentVariable($name, $overrides[$name], "Process")
    }
    return $snapshot
}

function Pop-ReadOnlyGitEnvironment {
    param([hashtable]$Snapshot)

    foreach ($name in $Snapshot.Keys) {
        [Environment]::SetEnvironmentVariable($name, $Snapshot[$name], "Process")
    }
}

function Start-GitHealth {
    param([string]$PublishPath, [string]$DataPath, [int]$Port)

    $assembly = Join-Path $PublishPath "githealth.dll"
    if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) {
        throw "Publication GitHealth introuvable : $assembly"
    }

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = "dotnet"
    $startInfo.WorkingDirectory = $PublishPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    Set-AcceptanceProcessArguments $startInfo @(
        $assembly, "--no-browser", "--port", $Port.ToString(),
        "--data-dir", $DataPath
    )

    $process = [Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw "Le processus GitHealth n'a pas démarré."
    }
    return $process
}

function Stop-GitHealth {
    param([Diagnostics.Process]$Process)

    if (-not $Process.HasExited) {
        $killTree = [Diagnostics.Process].GetMethod("Kill", [type[]]@([bool]))
        if ($null -ne $killTree) {
            [void]$killTree.Invoke($Process, @($true))
        }
        else {
            $Process.Kill()
        }
        $Process.WaitForExit()
    }
    $Process.Dispose()
}
