namespace App.GitHealth.Api.Features.Runtime;

internal sealed record RuntimeInfoResponse
{
    public string? InitialRepositoryPath { get; init; }

    public string? RepositoriesRoot { get; init; }

    public required bool CanBrowseDirectories { get; init; }

    public required string Mode { get; init; }

    /// <summary>Git answers at startup. False, no analysis can succeed.</summary>
    public required bool IsGitAvailable { get; init; }

    /// <summary>Git executable selected, or <see langword="null" /> if none was found.</summary>
    public string? GitExecutablePath { get; init; }

    /// <summary>Git version, or the reason it is unavailable.</summary>
    public required string GitDiagnostic { get; init; }
}
