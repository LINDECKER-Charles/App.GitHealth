namespace App.GitHealth.Api.Features.Runtime;

internal sealed record DirectoryEntryResponse
{
    public required string Name { get; init; }

    public required string Path { get; init; }
}
