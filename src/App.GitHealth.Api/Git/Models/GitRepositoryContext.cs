namespace App.GitHealth.Api.Git.Models;

internal sealed record GitRepositoryContext
{
    public GitRepositoryContext(
        string invocationPath,
        string gitDirectory,
        string? workingTreePath)
    {
        InvocationPath = invocationPath;
        WorkingTreePath = workingTreePath is null ? null : ResolvePhysicalPath(workingTreePath);
        CanonicalPath = WorkingTreePath ?? ResolvePhysicalPath(invocationPath);
        GitDirectory = ResolvePhysicalPath(gitDirectory);
    }

    public string InvocationPath { get; }

    public string CanonicalPath { get; }

    public string GitDirectory { get; }

    public string? WorkingTreePath { get; }

    private static string ResolvePhysicalPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var target = new DirectoryInfo(fullPath).ResolveLinkTarget(returnFinalTarget: true);
        return target?.FullName ?? fullPath;
    }
}
