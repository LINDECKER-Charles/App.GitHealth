namespace App.GitHealth.Api.Features.Runtime;

internal sealed record RuntimeInfoResponse
{
    public string? RepositoriesRoot { get; init; }

    public required bool CanBrowseDirectories { get; init; }

    public required string Mode { get; init; }
}
