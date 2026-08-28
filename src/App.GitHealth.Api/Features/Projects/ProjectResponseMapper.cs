using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Api.Features.Projects;

internal static class ProjectResponseMapper
{
    public static ProjectResponse Map(ProjectEntity entity)
    {
        var project = entity.ToDomain();
        return new ProjectResponse
        {
            Id = entity.Id,
            DisplayName = entity.DisplayName,
            RepositoryPath = entity.RepositoryPath,
            IsRepositoryAccessible = entity.IsRepositoryAccessible,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            ReferenceName = project.Settings.Reference?.FullName,
            BranchNamespace = project.Settings.BranchNamespace,
            ActiveUntilDays = project.Settings.Thresholds.ActiveUntilDays,
            InactiveAfterDays = project.Settings.Thresholds.InactiveAfterDays,
            ExcludedPatterns = project.Settings.Policy.ExcludedPatterns,
            ProtectedPatterns = project.Settings.Policy.ProtectedPatterns,
            LastSuccessfulAnalysisId = entity.LastSuccessfulAnalysisId,
        };
    }

    public static RepositoryValidationResponse Map(RepositoryDescriptor descriptor) => new()
    {
        CanonicalPath = descriptor.Location.CanonicalPath,
        IsBare = descriptor.Location.IsBare,
        SuggestedReference = descriptor.SuggestedReference?.FullName,
        References = descriptor.References.Select(reference => reference.FullName).ToArray(),
    };
}
