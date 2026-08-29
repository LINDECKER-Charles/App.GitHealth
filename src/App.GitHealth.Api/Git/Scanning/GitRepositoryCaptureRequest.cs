namespace App.GitHealth.Api.Git.Scanning;

internal sealed record GitRepositoryCaptureRequest(
    string RepositoryPath,
    string? RepositoriesRoot);
