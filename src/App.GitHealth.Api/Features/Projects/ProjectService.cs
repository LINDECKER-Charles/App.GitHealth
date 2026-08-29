using App.GitHealth.Api.Features.Analyses;
using App.GitHealth.Api.Features.Common;
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
            return Invalid<ProjectResponse>("Le nom du projet est absent ou trop long.");
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
                "Le projet est occupé par une analyse ou une relocalisation en cours."));
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
        if (!ContainsConfiguredReference(stored.ReferenceName, descriptor))
        {
            return ApiOutcome<ProjectResponse>.Failed(ApiProblems.BadRequest(
                ApiErrorCodes.InvalidReference,
                "La référence configurée n’existe pas dans le dépôt relocalisé."));
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
                "Un projet utilise déjà ce dépôt."));
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
                "Un projet utilise déjà ce dépôt."));
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
            "Le dépôt relocalisé ne contient pas le dernier commit de référence connu.");
    }

    private static ApiOutcome<ProjectSettings> BuildSettings(
        ProjectSettingsRequest? request,
        RepositoryDescriptor descriptor)
    {
        request ??= new ProjectSettingsRequest();
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.BranchNamespace);
            var reference = SelectReference(request.ReferenceName, descriptor);
            return ApiOutcome<ProjectSettings>.Success(new ProjectSettings
            {
                Reference = reference,
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

    private static GitRef SelectReference(string? requested, RepositoryDescriptor descriptor)
    {
        var reference = requested is null ? descriptor.SuggestedReference : new GitRef(requested);
        if (reference is null || !descriptor.References.Contains(reference))
        {
            throw new ArgumentException("La référence choisie n’existe pas dans le dépôt.");
        }

        return reference;
    }

    private static bool ContainsConfiguredReference(
        string? referenceName,
        RepositoryDescriptor descriptor) =>
        referenceName is null || descriptor.References.Contains(new GitRef(referenceName));

    private static ApiOutcome<T> Invalid<T>(string detail) =>
        ApiOutcome<T>.Failed(ApiProblems.BadRequest(ApiErrorCodes.InvalidRequest, detail));

    private static ApiFailure ProjectNotFound() => ApiProblems.NotFound(
        ApiErrorCodes.ProjectNotFound,
        "Le projet demandé n’existe pas.");
}
