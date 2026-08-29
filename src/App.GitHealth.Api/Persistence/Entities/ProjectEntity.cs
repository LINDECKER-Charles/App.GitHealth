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

    public ICollection<AnalysisRunEntity> AnalysisRuns { get; } = [];

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
            Reference = ReferenceName is null ? null : new GitRef(ReferenceName),
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

    private ProjectEntity ApplySettings(ProjectSettings settings)
    {
        ReferenceName = settings.Reference?.FullName;
        BranchNamespace = settings.BranchNamespace;
        ActiveUntilDays = settings.Thresholds.ActiveUntilDays;
        InactiveAfterDays = settings.Thresholds.InactiveAfterDays;
        ExcludedPatternsJson = JsonSerializer.Serialize(settings.Policy.ExcludedPatterns);
        ProtectedPatternsJson = JsonSerializer.Serialize(settings.Policy.ProtectedPatterns);
        return this;
    }

    private static string[] DeserializePatterns(string json) =>
        JsonSerializer.Deserialize<string[]>(json) ?? [];
}
