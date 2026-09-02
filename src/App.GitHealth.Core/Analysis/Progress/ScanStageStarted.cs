namespace App.GitHealth.Core.Analysis;

/// <summary>The scan moves on to a new stage; every reference restarts from zero there.</summary>
public sealed record ScanStageStarted(RepositoryScanStage Stage) : RepositoryScanEvent;
