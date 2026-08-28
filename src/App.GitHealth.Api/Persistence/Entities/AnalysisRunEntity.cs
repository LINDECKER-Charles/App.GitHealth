using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Api.Persistence.Entities;

internal sealed class AnalysisRunEntity
{
    private AnalysisRunEntity()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public ProjectEntity Project { get; private set; } = null!;

    public AnalysisRunStatus Status { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public DateTimeOffset? CapturedAtUtc { get; private set; }

    public string? GitVersion { get; private set; }

    public string ReferenceName { get; private set; } = string.Empty;

    public string? ReferenceCommit { get; private set; }

    public string BranchNamespace { get; private set; } = string.Empty;

    public int ActiveUntilDays { get; private set; }

    public int InactiveAfterDays { get; private set; }

    public string ExcludedPatternsJson { get; private set; } = "[]";

    public string ProtectedPatternsJson { get; private set; } = "[]";

    public string? FailureCode { get; private set; }

    public string? FailureMessage { get; private set; }

    public ICollection<BranchSnapshotEntity> Branches { get; } = [];

    public static AnalysisRunEntity Start(ProjectEntity project, DateTimeOffset startedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(project);
        UtcDate.Require(startedAtUtc, nameof(startedAtUtc));
        var referenceName = project.ReferenceName
            ?? throw new InvalidOperationException("Le projet ne possède pas de référence Git.");
        return new AnalysisRunEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Status = AnalysisRunStatus.Running,
            StartedAtUtc = startedAtUtc,
            ReferenceName = referenceName,
            BranchNamespace = project.BranchNamespace,
            ActiveUntilDays = project.ActiveUntilDays,
            InactiveAfterDays = project.InactiveAfterDays,
            ExcludedPatternsJson = project.ExcludedPatternsJson,
            ProtectedPatternsJson = project.ProtectedPatternsJson,
        };
    }

    public void Complete(AnalysisCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        EnsureRunning();
        UtcDate.Require(completion.CompletedAtUtc, nameof(completion));
        var scan = completion.Scan;
        CapturedAtUtc = scan.Metadata.CapturedAt;
        CompletedAtUtc = completion.CompletedAtUtc;
        GitVersion = scan.Metadata.GitVersion;
        ReferenceCommit = scan.ReferenceCommit.Value;
        Status = AnalysisRunStatus.Completed;
        AddBranches(scan.Branches);
    }

    public void Fail(AnalysisFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        EnsureRunning();
        ArgumentException.ThrowIfNullOrWhiteSpace(failure.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(failure.Message);
        UtcDate.Require(failure.FailedAtUtc, nameof(failure));
        CompletedAtUtc = failure.FailedAtUtc;
        FailureCode = failure.Code.Trim();
        FailureMessage = failure.Message.Trim();
        Status = failure.IsCancellation
            ? AnalysisRunStatus.Cancelled
            : AnalysisRunStatus.Failed;
    }

    private void AddBranches(IEnumerable<ScannedBranch> branches)
    {
        foreach (var branch in branches)
        {
            Branches.Add(BranchSnapshotEntity.Create(Id, branch));
        }
    }

    private void EnsureRunning()
    {
        if (Status != AnalysisRunStatus.Running)
        {
            throw new InvalidOperationException("Seule une analyse en cours peut être terminée.");
        }
    }
}
