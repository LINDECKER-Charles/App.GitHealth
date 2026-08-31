function Get-AllBranches {
    param([hashtable]$Context, [string]$ProjectId)

    $items = @()
    $cursor = $null
    do {
        $uri = "$($Context.BaseAddress)/api/projects/$ProjectId" +
            "/analyses/latest/branches?pageSize=200"
        if (-not [string]::IsNullOrWhiteSpace($cursor)) {
            $uri += "&cursor=$([uri]::EscapeDataString($cursor))"
        }
        $page = Invoke-RestMethod $uri -WebSession $Context.Session -TimeoutSec 30
        $items += @($page.items)
        $cursor = $page.nextCursor
    } while (-not [string]::IsNullOrWhiteSpace($cursor))
    return $items
}

function Get-CategoryCounts {
    param([object[]]$Items, [string]$Property)

    return @($Items | Group-Object -Property $Property | Sort-Object Name |
        ForEach-Object { [ordered]@{ name = $_.Name; count = $_.Count } })
}

function Assert-GitMetrics {
    param([string]$Repository, [string]$Reference, [object[]]$Items)

    if ($Items.Count -eq 0) {
        throw "The real acceptance run produced no branch."
    }
    $checked = 0
    foreach ($item in $Items | Select-Object -First 5) {
        $countLine = @(Invoke-GitRead $Repository @(
            "rev-list", "--left-right", "--count",
            "$Reference...$($item.referenceName)"
        ))[0]
        $counts = $countLine -split "\s+"
        if ([int]$counts[0] -ne $item.behindCount `
            -or [int]$counts[1] -ne $item.aheadCount) {
            throw "The Git metrics do not match the snapshot."
        }
        $checked++
    }
    return $checked
}

function Add-RealProject {
    param([hashtable]$Context, [string]$Repository, [object]$Recipe)

    $settings = @{
        referenceName = $Recipe.Configuration.Reference
        branchNamespace = $Recipe.Configuration.Namespace
        activeUntilDays = 30
        inactiveAfterDays = 90
        excludedPatterns = @()
        protectedPatterns = @($Recipe.Configuration.Reference)
    }
    return Invoke-ApiMutation $Context @{
        Method = "Post"
        Path = "/api/projects"
        Body = @{
            displayName = "Real acceptance $($Recipe.Index)"
            repositoryPath = $Repository
            settings = $settings
        }
    }
}

function Invoke-MeasuredAnalysis {
    param([hashtable]$Context, [string]$Repository, [object]$Project)

    $timer = [Diagnostics.Stopwatch]::StartNew()
    $launch = Invoke-ApiMutation $Context @{
        Method = "Post"
        Path = "/api/projects/$($Project.Id)/analyses"
    }
    Wait-ForAnalysis $Context.BaseAddress $Context.Session $launch.analysisId
    $timer.Stop()
    $items = @(Get-AllBranches $Context $Project.Id)
    $checked = Assert-GitMetrics $Repository $Project.Reference $items
    Invoke-WebRequest $Project.CsvUri -WebSession $Context.Session `
        -OutFile $Project.CsvPath -UseBasicParsing
    return [PSCustomObject]@{
        AnalysisId = $launch.analysisId
        BranchCount = $items.Count
        MetricsCompared = $checked
        DurationMilliseconds = $timer.ElapsedMilliseconds
        Topologies = @(Get-CategoryCounts $items "topology")
        Activities = @(Get-CategoryCounts $items "activity")
    }
}

function Invoke-RepositoryRecipe {
    param([hashtable]$Context, [string]$Repository, [int]$Index)

    $configuration = Get-RepositoryConfiguration $Repository
    $before = Get-RepositoryFingerprint $Repository
    $headCommit = @(Invoke-GitRead $Repository @("rev-parse", "HEAD"))[0]
    $commitLine = @(Invoke-GitRead $Repository @("rev-list", "--all", "--count"))[0]
    $commits = [int]$commitLine
    $files = (Invoke-GitRead $Repository @("ls-files")).Count
    $recipe = [PSCustomObject]@{ Index = $Index; Configuration = $configuration }
    $created = Add-RealProject $Context $Repository $recipe
    $baseUri = "$($Context.BaseAddress)/api/projects/$($created.id)"
    $project = [PSCustomObject]@{
        Id = $created.id
        Reference = $configuration.Reference
        CsvPath = Join-Path $Context.OutputPath "branches-$Index.csv"
        CsvUri = "$baseUri/analyses/latest/branches.csv"
    }
    $measurement = Invoke-MeasuredAnalysis $Context $Repository $project
    return New-RecipeResult $project $measurement @{
        Index = $Index; Before = $before; Commits = $commits; Files = $files
        References = $configuration.BranchCount; HeadCommit = $headCommit
    }
}

function New-RecipeResult {
    param([object]$Project, [object]$Measurement, [hashtable]$Source)

    return [PSCustomObject]@{
        ProjectId = $Project.Id
        AnalysisId = $Measurement.AnalysisId
        Before = $Source.Before
        Evidence = [ordered]@{
            label = "real-repository-$($Source.Index)"
            headCommit = $Source.HeadCommit
            commitCount = $Source.Commits
            trackedFileCount = $Source.Files
            referenceCount = $Source.References
            analyzedBranchCount = $Measurement.BranchCount
            metricsCompared = $Measurement.MetricsCompared
            durationMilliseconds = $Measurement.DurationMilliseconds
            topologies = $Measurement.Topologies
            activities = $Measurement.Activities
            repositoryUnchanged = $false
        }
    }
}

function Assert-RealRepositoryCoverage {
    param([object[]]$Recipes)

    $topologies = @($Recipes | ForEach-Object Evidence |
        ForEach-Object topologies | ForEach-Object name)
    foreach ($required in @("Merged", "Diverged")) {
        if ($topologies -notcontains $required) {
            throw "The real topology '$required' was not exercised."
        }
    }
    $activities = @($Recipes | ForEach-Object Evidence |
        ForEach-Object activities | ForEach-Object name)
    if ($activities -notcontains "Inactive") {
        throw "No real inactive branch was exercised."
    }
}

function Assert-RestartedSnapshots {
    param([hashtable]$Context, [object[]]$Recipes)

    $projects = Invoke-RestMethod "$($Context.BaseAddress)/api/projects" `
        -WebSession $Context.Session
    foreach ($recipe in $Recipes) {
        if ($projects.id -notcontains $recipe.ProjectId) {
            throw "A real project disappeared after restart."
        }
        $uri = "$($Context.BaseAddress)/api/projects/$($recipe.ProjectId)" +
            "/analyses/latest/branches?pageSize=1"
        $page = Invoke-RestMethod $uri -WebSession $Context.Session
        if ($page.analysisId -ne $recipe.AnalysisId -or $page.items.Count -eq 0) {
            throw "The last real snapshot disappeared after restart."
        }
    }
}
