using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Git.Scanning;

/// <summary>
/// Narrates a scan to whoever follows it. Reporting to nobody is a valid scan, so the
/// readers never have to ask whether anyone is listening.
/// </summary>
internal sealed class ScanReporter(IProgress<RepositoryScanEvent>? progress)
{
    public static ScanReporter Silent { get; } = new(progress: null);

    public bool IsFollowed => progress is not null;

    public void StageStarted(RepositoryScanStage stage) =>
        progress?.Report(new ScanStageStarted(stage));

    public void ReferencesListed(IReadOnlyList<ScannedReferenceListing> references) =>
        progress?.Report(new ScanReferencesListed(references));

    public void ReferenceStarted(GitRef reference, RepositoryScanStage stage) =>
        progress?.Report(new ScanReferenceStarted(reference.FullName, stage));

    public void ReferenceMeasured(
        GitRef reference,
        BranchDivergence divergence,
        string? mergeBaseCommit)
    {
        progress?.Report(new ScanReferenceMeasured
        {
            ReferenceName = reference.FullName,
            Divergence = divergence,
            MergeBaseCommit = mergeBaseCommit,
        });
    }

    public void ReferenceEnriched(GitRef reference, IReadOnlyList<Contributor> contributors)
    {
        progress?.Report(new ScanReferenceEnriched
        {
            ReferenceName = reference.FullName,
            TopContributor = contributors.Count == 0 ? null : contributors[0].Name,
            ContributorCount = contributors.Count,
        });
    }

    public void CommandCompleted(ScanCommandCompleted command) => progress?.Report(command);
}
