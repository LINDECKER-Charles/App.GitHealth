namespace App.GitHealth.Api.Features.Runtime;

internal sealed record RuntimeInfoResponse
{
    public string? InitialRepositoryPath { get; init; }

    public string? RepositoriesRoot { get; init; }

    public required bool CanBrowseDirectories { get; init; }

    public required string Mode { get; init; }

    /// <summary>Git répond au démarrage. Faux, aucune analyse ne peut aboutir.</summary>
    public required bool IsGitAvailable { get; init; }

    /// <summary>Exécutable Git retenu, ou <see langword="null" /> s'il est introuvable.</summary>
    public string? GitExecutablePath { get; init; }

    /// <summary>Version de Git, ou la raison de son indisponibilité.</summary>
    public required string GitDiagnostic { get; init; }
}
