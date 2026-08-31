using System.Security;
using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Git.Paths;

namespace App.GitHealth.Api.Features.Runtime;

internal static class DirectoryBrowser
{
    internal const int MaximumDirectoryCount = 250;

    public static ApiOutcome<DirectoryListingResponse> Browse(
        string? requestedPath,
        string? allowedRoot)
    {
        try
        {
            var resolvedPath = ResolveRequestedPath(requestedPath, allowedRoot);
            if (!RepositoryPathGuard.IsAllowed(allowedRoot, resolvedPath))
            {
                return Failure(ApiProblems.Forbidden(
                    ApiErrorCodes.DirectoryNotAllowed,
                    "The requested folder is outside the allowed root."));
            }

            var directory = new DirectoryInfo(resolvedPath);
            if (!directory.Exists)
            {
                return Failure(ApiProblems.NotFound(
                    ApiErrorCodes.DirectoryNotFound,
                    "The requested folder does not exist."));
            }

            return ApiOutcome<DirectoryListingResponse>.Success(
                CreateListing(directory, allowedRoot));
        }
        catch (Exception exception) when (IsAccessFailure(exception))
        {
            return Failure(ApiProblems.Forbidden(
                ApiErrorCodes.DirectoryInaccessible,
                "The requested folder is not accessible."));
        }
        catch (Exception exception) when (IsInvalidPathFailure(exception))
        {
            return Failure(ApiProblems.BadRequest(
                ApiErrorCodes.InvalidDirectory,
                "The folder path is invalid."));
        }
    }

    private static DirectoryListingResponse CreateListing(
        DirectoryInfo directory,
        string? allowedRoot)
    {
        var directories = ReadAccessibleDirectories(
            directory,
            allowedRoot,
            out var isTruncated);
        return new DirectoryListingResponse
        {
            CurrentPath = directory.FullName,
            ParentPath = AllowedParent(directory.Parent, allowedRoot),
            Directories = directories,
            IsTruncated = isTruncated,
        };
    }

    private static DirectoryEntryResponse[] ReadAccessibleDirectories(
        DirectoryInfo directory,
        string? allowedRoot,
        out bool isTruncated)
    {
        var entries = new List<DirectoryEntryResponse>(MaximumDirectoryCount);
        isTruncated = false;
        foreach (var candidate in directory.EnumerateDirectories())
        {
            if (!CanBrowse(candidate, allowedRoot))
            {
                continue;
            }

            if (entries.Count == MaximumDirectoryCount)
            {
                isTruncated = true;
                break;
            }

            entries.Add(Map(candidate));
        }

        return entries
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static DirectoryEntryResponse Map(DirectoryInfo directory) => new()
    {
        Name = directory.Name,
        Path = directory.FullName,
    };

    private static bool CanBrowse(DirectoryInfo directory, string? allowedRoot)
    {
        try
        {
            if (!RepositoryPathGuard.IsAllowed(allowedRoot, directory.FullName))
            {
                return false;
            }

            using var enumerator = directory.EnumerateFileSystemInfos().GetEnumerator();
            _ = enumerator.MoveNext();
            return true;
        }
        catch (Exception exception) when (IsAccessFailure(exception)
            || IsInvalidPathFailure(exception))
        {
            return false;
        }
    }

    private static string ResolveRequestedPath(string? requestedPath, string? allowedRoot)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
        {
            return Path.GetFullPath(requestedPath);
        }

        if (!string.IsNullOrWhiteSpace(allowedRoot))
        {
            return Path.GetFullPath(allowedRoot);
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(userProfile)
            ? Environment.CurrentDirectory
            : userProfile;
    }

    private static string? AllowedParent(DirectoryInfo? parent, string? allowedRoot)
    {
        if (parent is null)
        {
            return null;
        }

        return RepositoryPathGuard.IsAllowed(allowedRoot, parent.FullName)
            ? parent.FullName
            : null;
    }

    private static bool IsAccessFailure(Exception exception) =>
        exception is UnauthorizedAccessException or SecurityException;

    private static bool IsInvalidPathFailure(Exception exception) =>
        exception is ArgumentException or IOException or NotSupportedException;

    private static ApiOutcome<DirectoryListingResponse> Failure(ApiFailure failure) =>
        ApiOutcome<DirectoryListingResponse>.Failed(failure);
}
