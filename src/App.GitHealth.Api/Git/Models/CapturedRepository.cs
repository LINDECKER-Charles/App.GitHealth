namespace App.GitHealth.Api.Git.Models;

internal sealed record CapturedRepository(
    GitRepositoryContext Context,
    string GitVersion,
    IReadOnlyDictionary<string, CapturedReference> References);
