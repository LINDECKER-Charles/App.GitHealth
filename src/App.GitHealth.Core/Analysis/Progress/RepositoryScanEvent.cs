namespace App.GitHealth.Core.Analysis;

/// <summary>
/// Something a scan has just done, reported while it runs. A reader follows a long scan
/// through these events; none of them changes what the scan finally returns.
/// </summary>
public abstract record RepositoryScanEvent;
