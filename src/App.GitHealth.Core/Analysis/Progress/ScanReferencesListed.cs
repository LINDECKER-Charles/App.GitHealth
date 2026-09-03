namespace App.GitHealth.Core.Analysis;

/// <summary>
/// Every reference the scan is going to read, in the order it will read them. Reported once,
/// as soon as the repository has been enumerated: it is the ledger the next events fill in.
/// </summary>
public sealed record ScanReferencesListed(IReadOnlyList<ScannedReferenceListing> References)
    : RepositoryScanEvent;
