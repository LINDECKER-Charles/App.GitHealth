namespace App.GitHealth.Core.Analysis;

/// <summary>
/// A reference is being read for the current stage. Several can be open at once: the scan
/// compares references in parallel when Git allows it.
/// </summary>
public sealed record ScanReferenceStarted(string ReferenceName, RepositoryScanStage Stage)
    : RepositoryScanEvent;
