using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Features.Snapshots;
using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Core.Assistant;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.Assistant;

/// <summary>
/// Turns a capture already in the database into the text an agent is handed. It reads
/// nothing from the repository: the assistant works from measurements taken earlier, which
/// is what keeps it as read-only as the rest of GitHealth.
/// </summary>
internal sealed class AssistantBriefingService(
    SnapshotService snapshots,
    IProjectRepository projects,
    IOptions<AssistantOptions> options)
{
    /// <summary>
    /// Oldest activity first. The ordering matters because the table is capped: what falls
    /// off the end is the most recently touched branches, which are the ones a reader is
    /// least likely to be asking about when they ask what can go.
    /// </summary>
    private static readonly SnapshotFilterParameters OldestFirst = new()
    {
        Sort = "activity",
        Direction = "asc",
    };

    public async Task<ApiOutcome<AssistantCapture>> BuildAsync(
        Guid projectId,
        string? baseline,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ApiOutcome<AssistantCapture>.Failed(ApiProblems.NotFound(
                ApiErrorCodes.ProjectNotFound,
                "The requested project does not exist."));
        }

        var selection = await snapshots.GetSelectionAsync(
            projectId,
            OldestFirst with { Baseline = baseline },
            cancellationToken);
        return selection.IsSuccess
            ? ApiOutcome<AssistantCapture>.Success(Compose(project, selection.Value!))
            : ApiOutcome<AssistantCapture>.Failed(selection.Failure!);
    }

    private AssistantCapture Compose(ProjectEntity project, SnapshotSelectionData selection)
    {
        var cap = options.Value.MaximumBranches;
        var branches = selection.Branches.Take(cap).Select(Describe).ToArray();
        var briefing = new AnalysisBriefing
        {
            RepositoryName = project.DisplayName,
            Baseline = selection.Analysis.ReferenceName,
            CapturedAt = selection.Analysis.CapturedAtUtc ?? selection.Analysis.StartedAtUtc,
            Policy = MapPolicy(selection.Policy),
            Branches = branches,
            OmittedBranchCount = Math.Max(0, selection.Branches.Count - branches.Length),
        };
        return new AssistantCapture
        {
            AnalysisId = selection.Analysis.Id,
            Briefing = briefing,
            ConsentGrantedAtUtc = project.AssistantConsentAtUtc,
        };
    }

    private static BriefingBranch Describe(ClassifiedSnapshot classified)
    {
        var branch = SnapshotMapper.Map(classified);
        return new BriefingBranch
        {
            ReferenceName = branch.ReferenceName,
            AheadCount = branch.AheadCount,
            BehindCount = branch.BehindCount,
            Relationship = BriefingLabel.Words(branch.Relationship),
            Topology = BriefingLabel.Words(branch.Topology),
            Activity = BriefingLabel.Words(branch.Activity),
            Recommendation = BriefingLabel.Words(branch.Recommendation),
            Reason = branch.Reason,
            LastActivityAt = branch.LastActivityAtUtc,
            TipAuthor = branch.TipAuthor,
            IsProtected = branch.IsProtected,
            IsExcluded = branch.IsExcluded,
        };
    }

    private static BriefingPolicy MapPolicy(SnapshotPolicyResponse policy) => new()
    {
        ActiveUntilDays = policy.ActiveUntilDays,
        InactiveAfterDays = policy.InactiveAfterDays,
        ProtectedPatterns = policy.ProtectedPatterns,
        ExcludedPatterns = policy.ExcludedPatterns,
    };
}
