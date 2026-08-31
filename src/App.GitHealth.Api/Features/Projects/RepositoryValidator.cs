using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Git.Paths;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.Projects;

internal sealed class RepositoryValidator(
    IRepositoryScanner scanner,
    IOptions<RepositoryAccessOptions> options)
{
    internal const int MaximumPathLength = 32768;

    public async Task<ApiOutcome<RepositoryDescriptor>> ValidateAsync(
        string? repositoryPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath)
            || repositoryPath.Length > MaximumPathLength)
        {
            return ApiOutcome<RepositoryDescriptor>.Failed(ApiProblems.BadRequest(
                ApiErrorCodes.InvalidPath,
                "The repository path is missing or too long."));
        }

        var accessFailure = CheckAccess(repositoryPath);
        if (accessFailure is not null)
        {
            return ApiOutcome<RepositoryDescriptor>.Failed(accessFailure);
        }

        var inspected = await scanner.InspectAsync(repositoryPath, cancellationToken);
        if (!inspected.TryGetValue(out var descriptor))
        {
            return ApiOutcome<RepositoryDescriptor>.Failed(
                ApiProblems.FromRepository(inspected.Error!));
        }

        accessFailure = CheckDescriptorAccess(descriptor);
        return accessFailure is null
            ? ApiOutcome<RepositoryDescriptor>.Success(descriptor)
            : ApiOutcome<RepositoryDescriptor>.Failed(accessFailure);
    }

    public async Task<ApiOutcome<bool>> ContainsCommitAsync(
        string repositoryPath,
        CommitId commit,
        CancellationToken cancellationToken)
    {
        var accessFailure = CheckAccess(repositoryPath);
        if (accessFailure is not null)
        {
            return ApiOutcome<bool>.Failed(accessFailure);
        }

        var result = await scanner.ContainsCommitAsync(
            repositoryPath,
            commit,
            cancellationToken);
        return result.TryGetValue(out var isPresent)
            ? ApiOutcome<bool>.Success(isPresent)
            : ApiOutcome<bool>.Failed(ApiProblems.FromRepository(result.Error!));
    }

    private ApiFailure? CheckAccess(string path)
    {
        try
        {
            return IsAllowed(path) ? null : ApiProblems.BadRequest(
                ApiErrorCodes.PathNotAllowed,
                "The repository is outside the allowed root.");
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException or UnauthorizedAccessException)
        {
            return ApiProblems.BadRequest(ApiErrorCodes.InvalidPath, exception.Message);
        }
    }

    private bool IsAllowed(string canonicalRepositoryPath)
    {
        return RepositoryPathGuard.IsAllowed(
            options.Value.RepositoriesRoot,
            canonicalRepositoryPath);
    }

    private ApiFailure? CheckDescriptorAccess(RepositoryDescriptor descriptor)
    {
        var paths = new[]
        {
            descriptor.Location.CanonicalPath,
            descriptor.Location.GitDirectory,
            descriptor.Location.WorkingTreePath,
        };
        foreach (var path in paths.Where(path => path is not null))
        {
            var failure = CheckAccess(path!);
            if (failure is not null)
            {
                return failure with
                {
                    Detail = "The repository or its Git metadata is outside the allowed root.",
                };
            }
        }

        return null;
    }
}
