namespace App.GitHealth.Core.Analysis;

public sealed record RepositoryLocation
{
    public RepositoryLocation(
        string canonicalPath,
        string gitDirectory,
        string? workingTreePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(gitDirectory);

        CanonicalPath = canonicalPath;
        GitDirectory = gitDirectory;
        WorkingTreePath = workingTreePath;
    }

    public string CanonicalPath { get; }

    public string GitDirectory { get; }

    public string? WorkingTreePath { get; }

    public bool IsBare => WorkingTreePath is null;
}
