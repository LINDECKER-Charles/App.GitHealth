namespace App.GitHealth.Api.Features.Projects;

internal sealed record ValidateRepositoryRequest(string? Path);

internal sealed record CreateProjectRequest
{
    public string? DisplayName { get; init; }

    public string? RepositoryPath { get; init; }

    public ProjectSettingsRequest? Settings { get; init; }
}

internal sealed record RelocateProjectRequest
{
    public string? RepositoryPath { get; init; }
}

internal sealed record ProjectOrganizationRequest
{
    public bool IsFavorite { get; init; }

    public string? GroupName { get; init; }
}

internal sealed record ProjectSettingsRequest
{
    public string? ReferenceName { get; init; }

    /// <summary>
    /// Full ordered baseline list. When absent, <see cref="ReferenceName"/> is used, which
    /// keeps every single-baseline caller working unchanged.
    /// </summary>
    public string[]? ReferenceNames { get; init; }

    public string BranchNamespace { get; init; } = "refs/heads/*";

    public int ActiveUntilDays { get; init; } = 30;

    public int InactiveAfterDays { get; init; } = 90;

    public string[] ExcludedPatterns { get; init; } = [];

    public string[] ProtectedPatterns { get; init; } = [];
}

internal sealed record ProjectCreation(
    CreateProjectRequest Request,
    App.GitHealth.Core.Analysis.RepositoryDescriptor Descriptor,
    App.GitHealth.Core.Projects.ProjectSettings Settings);

internal sealed record ProjectSettingsChange(
    Guid ProjectId,
    ProjectSettingsRequest Request,
    App.GitHealth.Core.Analysis.RepositoryDescriptor Descriptor);
