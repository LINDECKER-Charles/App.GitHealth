using System.Text.Json;
using App.GitHealth.Core.Branches;
using App.GitHealth.Core.Projects;

namespace App.GitHealth.Api.Persistence.Entities;

internal sealed class ProjectEntity
{
    private ProjectEntity()
    {
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string RepositoryPath { get; private set; } = string.Empty;

    public bool IsRepositoryAccessible { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public string? ReferenceName { get; private set; }

    public string BranchNamespace { get; private set; } = string.Empty;

    public int ActiveUntilDays { get; private set; }

    public int InactiveAfterDays { get; private set; }

    public string ExcludedPatternsJson { get; private set; } = "[]";

    public string ProtectedPatternsJson { get; private set; } = "[]";

    public bool IsFavorite { get; private set; }

    public string? GroupName { get; private set; }

    public Guid? LastSuccessfulAnalysisId { get; set; }

    /// <summary>
    /// When sending this repository's captures to an agent was allowed, or null while it
    /// never has been. It lives on the project because the question is asked once per
    /// repository and holds for every baseline of it.
    /// </summary>
    public DateTimeOffset? AssistantConsentAtUtc { get; private set; }

    public ICollection<AnalysisRunEntity> AnalysisRuns { get; } = [];

    /// <summary>
    /// Comparison baselines. Always eager-loaded: an empty collection here is indistinguishable
    /// from a project that declares none, and would silently reinsert every row on a save.
    /// </summary>
    public ICollection<ProjectBaselineEntity> Baselines { get; } = [];

    public static ProjectEntity Create(Project project, DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(project);
        UtcDate.Require(createdAtUtc, nameof(createdAtUtc));
        return new ProjectEntity
        {
            Id = project.Id,
            DisplayName = project.DisplayName,
            RepositoryPath = Path.GetFullPath(project.RepositoryPath),
            IsRepositoryAccessible = true,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        }.ApplySettings(project.Settings).ApplyOrganization(project.Organization);
    }

    public Project ToDomain()
    {
        var settings = new ProjectSettings
        {
            Baselines = ReadBaselines(),
            BranchNamespace = BranchNamespace,
            Thresholds = ActivityThresholds.Create(ActiveUntilDays, InactiveAfterDays),
            Policy = BranchPolicy.Create(
                DeserializePatterns(ExcludedPatternsJson),
                DeserializePatterns(ProtectedPatternsJson)),
        };
        return Project.Restore(Id, DisplayName, RepositoryPath) with
        {
            Settings = settings,
            Organization = new ProjectOrganization
            {
                IsFavorite = IsFavorite,
                GroupName = GroupName,
            },
        };
    }

    public void Relocate(string repositoryPath, DateTimeOffset changedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        UtcDate.Require(changedAtUtc, nameof(changedAtUtc));
        RepositoryPath = Path.GetFullPath(repositoryPath);
        IsRepositoryAccessible = true;
        UpdatedAtUtc = changedAtUtc;
    }

    public void MarkUnavailable(DateTimeOffset changedAtUtc)
    {
        UtcDate.Require(changedAtUtc, nameof(changedAtUtc));
        IsRepositoryAccessible = false;
        UpdatedAtUtc = changedAtUtc;
    }

    public void MarkAccessible(DateTimeOffset changedAtUtc)
    {
        UtcDate.Require(changedAtUtc, nameof(changedAtUtc));
        IsRepositoryAccessible = true;
        UpdatedAtUtc = changedAtUtc;
    }

    /// <summary>
    /// Records or withdraws the permission to send this repository's captures to an agent.
    /// Withdrawing keeps the conversations already stored: the consent governs what leaves
    /// the machine next, not what was read of it before.
    /// </summary>
    public void SetAssistantConsent(DateTimeOffset? grantedAtUtc, DateTimeOffset changedAtUtc)
    {
        UtcDate.Require(changedAtUtc, nameof(changedAtUtc));
        AssistantConsentAtUtc = grantedAtUtc;
        UpdatedAtUtc = changedAtUtc;
    }

    public void UpdateSettings(ProjectSettings settings, DateTimeOffset changedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(settings);
        UtcDate.Require(changedAtUtc, nameof(changedAtUtc));
        ApplySettings(settings);
        UpdatedAtUtc = changedAtUtc;
    }

    public void UpdateOrganization(
        ProjectOrganization organization,
        DateTimeOffset changedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(organization);
        UtcDate.Require(changedAtUtc, nameof(changedAtUtc));
        ApplyOrganization(organization);
        UpdatedAtUtc = changedAtUtc;
    }

    private ProjectEntity ApplyOrganization(ProjectOrganization organization)
    {
        IsFavorite = organization.IsFavorite;
        GroupName = organization.GroupName;
        return this;
    }

    /// <summary>
    /// Sole writer of <see cref="ReferenceName"/>, which is the denormalised primary baseline.
    /// Keeping the two in one statement is what stops them drifting apart.
    /// </summary>
    private ProjectEntity ApplySettings(ProjectSettings settings)
    {
        ReferenceName = settings.Reference?.FullName;
        BranchNamespace = settings.BranchNamespace;
        ActiveUntilDays = settings.Thresholds.ActiveUntilDays;
        InactiveAfterDays = settings.Thresholds.InactiveAfterDays;
        ExcludedPatternsJson = JsonSerializer.Serialize(settings.Policy.ExcludedPatterns);
        ProtectedPatternsJson = JsonSerializer.Serialize(settings.Policy.ProtectedPatterns);
        ApplyBaselines(settings.Baselines);
        return this;
    }

    /// <summary>
    /// Reconciles by reference name rather than rebuilding the list, so that reordering the
    /// baselines keeps each one's <see cref="ProjectBaselineEntity.LastSuccessfulAnalysisId"/>.
    /// </summary>
    private void ApplyBaselines(IReadOnlyList<GitRef> baselines)
    {
        var wanted = baselines.Select(baseline => baseline.FullName).ToArray();
        var removed = Baselines
            .Where(baseline => !wanted.Contains(baseline.ReferenceName, StringComparer.Ordinal))
            .ToArray();
        foreach (var baseline in removed)
        {
            Baselines.Remove(baseline);
        }

        for (var position = 0; position < wanted.Length; position++)
        {
            AttachBaseline(wanted[position], position);
        }

        PromoteLatestOfPrimaryBaseline();
    }

    /// <summary>
    /// The project-wide pointer always follows the primary baseline. Every writer that can move
    /// which baseline is primary — a completed run, a deleted capture, a reordered list — ends by
    /// calling this, which is what stops the pointer disagreeing with
    /// <see cref="ReferenceName"/> on the same row.
    /// </summary>
    public void PromoteLatestOfPrimaryBaseline()
    {
        LastSuccessfulAnalysisId = Baselines
            .OrderBy(baseline => baseline.Position)
            .Select(baseline => baseline.LastSuccessfulAnalysisId)
            .FirstOrDefault();
    }

    private void AttachBaseline(string referenceName, int position)
    {
        var existing = Baselines.SingleOrDefault(baseline =>
            string.Equals(baseline.ReferenceName, referenceName, StringComparison.Ordinal));
        if (existing is null)
        {
            Baselines.Add(ProjectBaselineEntity.Create(Id, referenceName, position));
            return;
        }

        existing.MoveTo(position);
    }

    private GitRef[] ReadBaselines() => Baselines
        .OrderBy(baseline => baseline.Position)
        .ThenBy(baseline => baseline.ReferenceName, StringComparer.Ordinal)
        .Select(baseline => new GitRef(baseline.ReferenceName))
        .ToArray();

    private static string[] DeserializePatterns(string json) =>
        JsonSerializer.Deserialize<string[]>(json) ?? [];
}
