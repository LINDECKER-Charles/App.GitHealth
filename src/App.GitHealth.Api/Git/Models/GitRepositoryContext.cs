using App.GitHealth.Api.Git.Paths;

namespace App.GitHealth.Api.Git.Models;

internal sealed record GitRepositoryContext
{
    public GitRepositoryContext(
        string invocationPath,
        string? workingTreePath,
        GitRepositoryMetadataPaths metadataPaths)
    {
        WorkingTreePath = workingTreePath is null
            ? null
            : RepositoryPathGuard.ResolvePhysicalPath(workingTreePath);
        CanonicalPath = WorkingTreePath
            ?? RepositoryPathGuard.ResolvePhysicalPath(invocationPath);
        InvocationPath = CanonicalPath;
        GitDirectory = metadataPaths.GitDirectory;
        CommonDirectory = metadataPaths.CommonDirectory;
        ObjectDirectory = metadataPaths.ObjectDirectory;
    }

    public string InvocationPath { get; }

    public string CanonicalPath { get; }

    public string GitDirectory { get; }

    public string CommonDirectory { get; }

    public string ObjectDirectory { get; }

    public string? WorkingTreePath { get; }
}
