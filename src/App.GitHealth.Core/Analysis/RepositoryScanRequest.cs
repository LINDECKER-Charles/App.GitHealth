using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Analysis;

public sealed record RepositoryScanRequest
{
    public RepositoryScanRequest(
        string repositoryPath,
        GitRef reference,
        string branchPattern = "refs/heads/*")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchPattern);

        RepositoryPath = repositoryPath;
        Reference = reference;
        BranchPattern = branchPattern;
    }

    public string RepositoryPath { get; }

    public GitRef Reference { get; }

    public string BranchPattern { get; }
}
