using App.GitHealth.Api.Features.Analyses;
using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Features.Projects;
using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Core.Branches;
using App.GitHealth.Core.Common;

namespace App.GitHealth.Api.Features.Baselines;

/// <summary>
/// Reads and replaces the branches a project compares itself against. The list is written
/// whole, so its order — which decides the primary baseline — is never ambiguous.
/// </summary>
internal sealed class BaselineService(
    IProjectRepository projects,
    IAnalysisRepository analyses,
    RepositoryValidator validator,
    AnalysisQueue queue,
    IClock clock)
{
    public async Task<ApiOutcome<BaselineListResponse>> ListAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var stored = await projects.GetAsync(projectId, cancellationToken);
        if (stored is null)
        {
            return ApiOutcome<BaselineListResponse>.Failed(ProjectNotFound());
        }

        var items = await DescribeAsync(stored, cancellationToken);
        return ApiOutcome<BaselineListResponse>.Success(new BaselineListResponse
        {
            Items = items,
            AvailableReferences = await ReadReferencesAsync(stored, cancellationToken),
        });
    }

    public async Task<ApiOutcome<ProjectResponse>> ReplaceAsync(
        Guid projectId,
        BaselineUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await using var reservation = await queue.TryReserveProjectAsync(
            projectId,
            cancellationToken);
        return reservation is null
            ? ApiOutcome<ProjectResponse>.Failed(ApiProblems.Conflict(
                ApiErrorCodes.ProjectBusy,
                "The project is busy with an analysis or a relocation in progress."))
            : await ReplaceReservedAsync(projectId, request, cancellationToken);
    }

    private async Task<ApiOutcome<ProjectResponse>> ReplaceReservedAsync(
        Guid projectId,
        BaselineUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var stored = await projects.GetAsync(projectId, cancellationToken);
        if (stored is null)
        {
            return ApiOutcome<ProjectResponse>.Failed(ProjectNotFound());
        }

        var validation = await validator.ValidateAsync(
            stored.RepositoryPath,
            cancellationToken);
        if (!validation.IsSuccess)
        {
            return ApiOutcome<ProjectResponse>.Failed(validation.Failure!);
        }

        var known = validation.Value!.References.Select(item => item.FullName).ToArray();
        var settings = Build(stored, request.ReferenceNames, known);
        return settings.IsSuccess
            ? await PersistAsync(projectId, settings.Value!, cancellationToken)
            : ApiOutcome<ProjectResponse>.Failed(settings.Failure!);
    }

    private async Task<ApiOutcome<ProjectResponse>> PersistAsync(
        Guid projectId,
        Core.Projects.ProjectSettings settings,
        CancellationToken cancellationToken)
    {
        var update = new ProjectSettingsUpdate(projectId, settings, clock.UtcNow);
        await projects.UpdateSettingsAsync(update, cancellationToken);
        var updated = await projects.GetAsync(projectId, cancellationToken);
        return ApiOutcome<ProjectResponse>.Success(ProjectResponseMapper.Map(updated!));
    }

    /// <summary>
    /// Only the baseline list changes: everything else is carried over with `with`, so a
    /// baseline edit can never reset the thresholds or the branch patterns.
    /// </summary>
    private static ApiOutcome<Core.Projects.ProjectSettings> Build(
        ProjectEntity stored,
        string[] requested,
        string[] known)
    {
        if (requested.Length == 0)
        {
            return Invalid("At least one baseline is required.");
        }

        var unknown = requested.Except(known, StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0)
        {
            return ApiOutcome<Core.Projects.ProjectSettings>.Failed(ApiProblems.BadRequest(
                ApiErrorCodes.InvalidReference,
                $"The repository has no reference named '{unknown[0]}'."));
        }

        try
        {
            var current = stored.ToDomain().Settings;
            return ApiOutcome<Core.Projects.ProjectSettings>.Success(current with
            {
                Baselines = requested.Select(name => new GitRef(name)).ToArray(),
            });
        }
        catch (ArgumentException exception)
        {
            return Invalid(exception.Message);
        }
    }

    private async Task<BaselineResponse[]> DescribeAsync(
        ProjectEntity stored,
        CancellationToken cancellationToken)
    {
        var ordered = stored.Baselines.OrderBy(baseline => baseline.Position).ToArray();
        var described = new List<BaselineResponse>(ordered.Length);
        foreach (var baseline in ordered)
        {
            described.Add(await DescribeOneAsync(stored.Id, baseline, cancellationToken));
        }

        return [.. described];
    }

    private async Task<BaselineResponse> DescribeOneAsync(
        Guid projectId,
        ProjectBaselineEntity baseline,
        CancellationToken cancellationToken)
    {
        var target = new AnalysisTarget(projectId, baseline.ReferenceName);
        var latest = await analyses.GetLastSuccessfulForBaselineAsync(target, cancellationToken);
        return new BaselineResponse
        {
            ReferenceName = baseline.ReferenceName,
            Position = baseline.Position,
            IsPrimary = baseline.Position == 0,
            LastSuccessfulAnalysisId = latest?.Id,
            LastCapturedAtUtc = latest?.CapturedAtUtc,
            BranchCount = latest?.Branches.Count ?? 0,
        };
    }

    /// <summary>An unreachable repository still has a configured list worth showing.</summary>
    private async Task<string[]> ReadReferencesAsync(
        ProjectEntity stored,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(stored.RepositoryPath, cancellationToken);
        return validation.IsSuccess
            ? validation.Value!.References.Select(reference => reference.FullName).ToArray()
            : [];
    }

    private static ApiOutcome<Core.Projects.ProjectSettings> Invalid(string detail) =>
        ApiOutcome<Core.Projects.ProjectSettings>.Failed(ApiProblems.BadRequest(
            ApiErrorCodes.InvalidRequest,
            detail));

    private static ApiFailure ProjectNotFound() => ApiProblems.NotFound(
        ApiErrorCodes.ProjectNotFound,
        "The requested project does not exist.");
}
