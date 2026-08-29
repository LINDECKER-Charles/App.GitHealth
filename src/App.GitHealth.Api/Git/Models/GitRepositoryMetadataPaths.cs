using App.GitHealth.Api.Git.Paths;

namespace App.GitHealth.Api.Git.Models;

internal sealed record GitRepositoryMetadataPaths
{
    public GitRepositoryMetadataPaths(
        string gitDirectory,
        string commonDirectory,
        string objectDirectory)
    {
        GitDirectory = RepositoryPathGuard.ResolvePhysicalPath(gitDirectory);
        CommonDirectory = RepositoryPathGuard.ResolvePhysicalPath(commonDirectory);
        ObjectDirectory = RepositoryPathGuard.ResolvePhysicalPath(objectDirectory);
    }

    public string GitDirectory { get; }

    public string CommonDirectory { get; }

    public string ObjectDirectory { get; }
}
