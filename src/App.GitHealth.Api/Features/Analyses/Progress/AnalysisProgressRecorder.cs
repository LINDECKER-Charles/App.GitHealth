using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Api.Features.Analyses;

/// <summary>
/// Writes what a scan reports into the live state of its analysis. The scan says what it
/// did; deciding which phase that means belongs here, on the analysis side.
/// </summary>
internal sealed class AnalysisProgressRecorder(AnalysisRunProgress progress)
    : IProgress<RepositoryScanEvent>
{
    public void Report(RepositoryScanEvent value)
    {
        switch (value)
        {
            case ScanStageStarted stage:
                progress.SetPhase(ToPhase(stage.Stage));
                break;
            case ScanReferencesListed listed:
                progress.List(listed.References);
                break;
            case ScanReferenceStarted started:
                progress.Start(started.ReferenceName, started.Stage);
                break;
            case ScanReferenceMeasured measured:
                progress.Measure(measured);
                break;
            case ScanReferenceEnriched enriched:
                progress.Enrich(enriched);
                break;
            case ScanCommandCompleted command:
                progress.Record(command);
                break;
            default:
                break;
        }
    }

    private static AnalysisPhase ToPhase(RepositoryScanStage stage) =>
        stage == RepositoryScanStage.Topology
            ? AnalysisPhase.Topology
            : AnalysisPhase.Enrichment;
}
