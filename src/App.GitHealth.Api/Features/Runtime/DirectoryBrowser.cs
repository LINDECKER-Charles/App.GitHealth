using System.Security;
using App.GitHealth.Api.Features.Common;

namespace App.GitHealth.Api.Features.Runtime;

internal static class DirectoryBrowser
{
    internal const int MaximumDirectoryCount = 250;

    public static ApiOutcome<DirectoryListingResponse> Browse(string? requestedPath)
    {
        try
        {
            var directory = new DirectoryInfo(ResolveRequestedPath(requestedPath));
            if (!directory.Exists)
            {
                return Failure(ApiProblems.NotFound(
                    ApiErrorCodes.DirectoryNotFound,
                    "Le dossier demandé n’existe pas."));
            }

            return ApiOutcome<DirectoryListingResponse>.Success(CreateListing(directory));
        }
        catch (Exception exception) when (IsAccessFailure(exception))
        {
            return Failure(ApiProblems.Forbidden(
                ApiErrorCodes.DirectoryInaccessible,
                "Le dossier demandé n’est pas accessible."));
        }
        catch (Exception exception) when (IsInvalidPathFailure(exception))
        {
            return Failure(ApiProblems.BadRequest(
                ApiErrorCodes.InvalidDirectory,
                "Le chemin du dossier est invalide."));
        }
    }

    private static DirectoryListingResponse CreateListing(DirectoryInfo directory)
    {
        var directories = ReadAccessibleDirectories(directory, out var isTruncated);
        return new DirectoryListingResponse
        {
            CurrentPath = directory.FullName,
            ParentPath = directory.Parent?.FullName,
            Directories = directories,
            IsTruncated = isTruncated,
        };
    }

    private static DirectoryEntryResponse[] ReadAccessibleDirectories(
        DirectoryInfo directory,
        out bool isTruncated)
    {
        var entries = new List<DirectoryEntryResponse>(MaximumDirectoryCount);
        isTruncated = false;
        foreach (var candidate in directory.EnumerateDirectories())
        {
            if (!CanBrowse(candidate))
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

    private static bool CanBrowse(DirectoryInfo directory)
    {
        try
        {
            using var enumerator = directory.EnumerateFileSystemInfos().GetEnumerator();
            _ = enumerator.MoveNext();
            return true;
        }
        catch (Exception exception) when (IsAccessFailure(exception)
            || exception is IOException)
        {
            return false;
        }
    }

    private static string ResolveRequestedPath(string? requestedPath)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
        {
            return Path.GetFullPath(requestedPath);
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(userProfile)
            ? Environment.CurrentDirectory
            : userProfile;
    }

    private static bool IsAccessFailure(Exception exception) =>
        exception is UnauthorizedAccessException or SecurityException;

    private static bool IsInvalidPathFailure(Exception exception) =>
        exception is ArgumentException or IOException or NotSupportedException;

    private static ApiOutcome<DirectoryListingResponse> Failure(ApiFailure failure) =>
        ApiOutcome<DirectoryListingResponse>.Failed(failure);
}
