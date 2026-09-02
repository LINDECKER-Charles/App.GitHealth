using App.GitHealth.Api.Features.Analyses;
using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;
using App.GitHealth.Core.Projects;
using Microsoft.EntityFrameworkCore;

namespace App.GitHealth.Api.Features.Projects;

internal sealed class ProjectService(
    RepositoryValidator validator,
    IProjectRepository projects,
    AnalysisQueue queue)
{
    internal const int MaximumDisplayNameLength = 200;

    public async Task<ApiOutcome<ProjectResponse>> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName)
            || request.DisplayName.Length > MaximumDisplayNameLength)
        {
            return Invalid<ProjectResponse>("The project name is missing or too long.");
        }

        var validation = await validator.ValidateAsync(request.RepositoryPath, cancellationToken);
        if (!validation.IsSuccess)
        {
            return ApiOutcome<ProjectResponse>.Failed(validation.Failure!);
        }

        var settings = BuildSettings(request.Settings, validation.Value!);
        if (!settings.IsSuccess)
        {
            return ApiOutcome<ProjectResponse>.Failed(settings.Failure!);
        }

        var creation = new ProjectCreation(request, validation.Value!, settings.Value!);
        return await PersistAsync(creation, cancellationToken);
    }

    public async Task<ApiOutcome<ProjectResponse>> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        return project is null
            ? ApiOutcome<ProjectResponse>.Failed(ProjectNotFound())
            : ApiOutcome<ProjectResponse>.Success(ProjectResponseMapper.Map(project));
    }

    public async Task<IReadOnlyList<ProjectResponse>> ListAsync(
        CancellationToken cancellationToken)
    {
        var stored = await projects.ListAsync(cancellationToken);
        return stored.Select(ProjectResponseMapper.Map).ToArray();
    }

    public async Task<ApiOutcome<ProjectResponse>> RelocateAsync(
        Guid projectId,
        RelocateProjectRequest request,
        CancellationToken cancellationToken)
    {
        await using var reservation = await queue.TryReserveProjectAsync(
            projectId,
            cancellationToken);
        if (reservation is null)
        {
            return ApiOutcome<ProjectResponse>.Failed(ApiProblems.Conflict(
                ApiErrorCodes.ProjectBusy,
                "The project is busy with an analysis or a relocation in progress."));
        }

        return await RelocateReservedAsync(projectId, request, cancellationToken);
    }

    private async Task<ApiOutcome<ProjectResponse>> RelocateReservedAsync(
        Guid projectId,
        RelocateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var stored = await projects.GetAsync(projectId, cancellationToken);
        if (stored is null)
        {
            return ApiOutcome<ProjectResponse>.Failed(ProjectNotFound());
        }

        var validation = await validator.ValidateAsync(request.RepositoryPath, cancellationToken);
        if (!validation.IsSuccess)
        {
            return ApiOutcome<ProjectResponse>.Failed(validation.Failure!);
        }

        var descriptor = validation.Value!;
        if (!ContainsConfiguredReferences(stored, descriptor))
        {
            return ApiOutcome<ProjectResponse>.Failed(ApiProblems.BadRequest(
                ApiErrorCodes.InvalidReference,
                "The configured baseline does not exist in the relocated repository."));
        }

        return await RelocateVerifiedAsync(projectId, descriptor, cancellationToken);
    }

    private async Task<ApiOutcome<ProjectResponse>> RelocateVerifiedAsync(
        Guid projectId,
        RepositoryDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var identityFailure = await CheckRepositoryIdentityAsync(
            projectId,
            descriptor.Location.CanonicalPath,
            cancellationToken);
        if (identityFailure is not null)
        {
            return ApiOutcome<ProjectResponse>.Failed(identityFailure);
        }

        return await PersistRelocationAsync(projectId, descriptor, cancellationToken);
    }

    public async Task<ApiOutcome<ProjectResponse>> UpdateAsync(
        Guid projectId,
        ProjectSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var stored = await projects.GetAsync(projectId, cancellationToken);
        if (stored is null)
        {
            return ApiOutcome<ProjectResponse>.Failed(ProjectNotFound());
        }

        var validation = await validator.ValidateAsync(stored.RepositoryPath, cancellationToken);
        if (!validation.IsSuccess)
        {
            return ApiOutcome<ProjectResponse>.Failed(validation.Failure!);
        }

        var change = new ProjectSettingsChange(projectId, request, validation.Value!);
        return await ApplySettingsAsync(change, cancellationToken);
    }

    private async Task<ApiOutcome<ProjectResponse>> PersistAsync(
        ProjectCreation creation,
        CancellationToken cancellationToken)
    {
        var project = Project.Create(
            creation.Request.DisplayName!,
            creation.Descriptor.Location.CanonicalPath)
            with
        {
            Settings = creation.Settings,
        };
        try
        {
            var stored = await projects.AddAsync(project, queue.UtcNow, cancellationToken);
            return ApiOutcome<ProjectResponse>.Success(ProjectResponseMapper.Map(stored));
        }
        catch (DbUpdateException)
        {
            return ApiOutcome<ProjectResponse>.Failed(ApiProblems.Conflict(
                ApiErrorCodes.ProjectAlreadyExists,
                "Another project already uses this repository."));
        }
    }

    private async Task<ApiOutcome<ProjectResponse>> ApplySettingsAsync(
        ProjectSettingsChange change,
        CancellationToken cancellationToken)
    {
        var settings = BuildSettings(change.Request, change.Descriptor);
        if (!settings.IsSuccess)
        {
            return ApiOutcome<ProjectResponse>.Failed(settings.Failure!);
        }

        var update = new ProjectSettingsUpdate(
            change.ProjectId,
            settings.Value!,
            queue.UtcNow);
        await projects.UpdateSettingsAsync(update, cancellationToken);
        return await GetAsync(change.ProjectId, cancellationToken);
    }

    private async Task<ApiOutcome<ProjectResponse>> PersistRelocationAsync(
        Guid projectId,
        RepositoryDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var relocation = new ProjectRelocation(
            projectId,
            descriptor.Location.CanonicalPath,
            queue.UtcNow);
        try
        {
            await projects.RelocateAsync(relocation, cancellationToken);
            return await GetAsync(projectId, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ApiOutcome<ProjectResponse>.Failed(ApiProblems.Conflict(
                ApiErrorCodes.ProjectAlreadyExists,
                "Another project already uses this repository."));
        }
    }

    private async Task<ApiFailure?> CheckRepositoryIdentityAsync(
        Guid projectId,
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var lastCommit = await projects.GetLastSuccessfulReferenceCommitAsync(
            projectId,
            cancellationToken);
        if (lastCommit is null)
        {
            return null;
        }

        var identity = await validator.ContainsCommitAsync(
            repositoryPath,
            new CommitId(lastCommit),
            cancellationToken);
        if (!identity.IsSuccess)
        {
            return identity.Failure;
        }

        return identity.Value ? null : ApiProblems.Conflict(
            ApiErrorCodes.RepositoryIdentityMismatch,
            "The relocated repository does not contain the last known baseline commit.");
    }

    private static ApiOutcome<ProjectSettings> BuildSettings(
        ProjectSettingsRequest? request,
        RepositoryDescriptor descriptor)
    {
        request ??= new ProjectSettingsRequest();
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.BranchNamespace);
            return ApiOutcome<ProjectSettings>.Success(new ProjectSettings
            {
                Baselines = SelectBaselines(request, descriptor),
                BranchNamespace = request.BranchNamespace,
                Thresholds = ActivityThresholds.Create(
                    request.ActiveUntilDays,
                    request.InactiveAfterDays),
                Policy = BranchPolicy.Create(
                    request.ExcludedPatterns,
                    request.ProtectedPatterns),
            });
        }
        catch (ArgumentException exception)
        {
            return Invalid<ProjectSettings>(exception.Message);
        }
    }

    /// <summary>
    /// The full list wins when it is given; otherwise the single reference, and failing that
    /// the repository's own suggestion. Every name must exist, or the project would declare
    /// a baseline nothing can be measured against.
    /// </summary>
    private static GitRef[] SelectBaselines(
        ProjectSettingsRequest request,
        RepositoryDescriptor descriptor)
    {
        var requested = request.ReferenceNames
            ?? (request.ReferenceName is null ? null : [request.ReferenceName]);
        if (requested is null)
        {
            return [RequireKnown(descriptor.SuggestedReference, descriptor)];
        }

        if (requested.Length == 0)
        {
            throw new ArgumentException("At least one baseline is required.");
        }

        return requested
            .Select(name => RequireKnown(new GitRef(name), descriptor))
            .ToArray();
    }

    private static GitRef RequireKnown(GitRef? reference, RepositoryDescriptor descriptor) =>
        reference is not null && descriptor.References.Contains(reference)
            ? reference
            : throw new ArgumentException(
                "The chosen baseline does not exist in the repository.");

    /// <summary>
    /// Every declared baseline must survive the move. A partial match would silently orphan
    /// one baseline's whole history.
    /// </summary>
    private static bool ContainsConfiguredReferences(
        ProjectEntity stored,
        RepositoryDescriptor descriptor) =>
        stored.ToDomain().Settings.Baselines.All(descriptor.References.Contains);

    private static ApiOutcome<T> Invalid<T>(string detail) =>
        ApiOutcome<T>.Failed(ApiProblems.BadRequest(ApiErrorCodes.InvalidRequest, detail));

    private static ApiFailure ProjectNotFound() => ApiProblems.NotFound(
        ApiErrorCodes.ProjectNotFound,
        "The requested project does not exist.");
}
