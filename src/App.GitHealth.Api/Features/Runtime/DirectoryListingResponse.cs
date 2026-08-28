namespace App.GitHealth.Api.Features.Runtime;

internal sealed record DirectoryListingResponse
{
    public required string CurrentPath { get; init; }

    public string? ParentPath { get; init; }

    public required IReadOnlyList<DirectoryEntryResponse> Directories { get; init; }

    public required bool IsTruncated { get; init; }
}
