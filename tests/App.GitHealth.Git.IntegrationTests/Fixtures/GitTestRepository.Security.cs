namespace App.GitHealth.Git.IntegrationTests.Fixtures;

internal sealed partial class GitTestRepository
{
    public string ResolveCommit(string reference) =>
        RunGit("rev-parse", $"{reference}^{{commit}}").Trim();

    public (string RootPath, string WorktreePath) CreateExternalCommonDirectoryScenario()
    {
        var allowedRoot = Path.Combine(_containerPath, "allowed-root");
        var worktreePath = Path.Combine(allowedRoot, "worktree");
        Directory.CreateDirectory(allowedRoot);
        RunGit("worktree", "add", worktreePath, "feature/ahead");
        var gitDirectory = RunExternal(
            worktreePath,
            ["rev-parse", "--absolute-git-dir"]).Trim();
        var allowedGitDirectory = Path.Combine(allowedRoot, "git-directory");
        Directory.Move(gitDirectory, allowedGitDirectory);
        RewriteWorktreeMetadata(worktreePath, allowedGitDirectory);
        return (allowedRoot, worktreePath);
    }

    public void ConfigureNestedExternalAlternate()
    {
        var internalDatabase = Path.Combine(RepositoryPath, "allowed-objects");
        var externalDatabase = Path.Combine(_containerPath, "external-objects");
        WriteAlternate(Path.Combine(RepositoryPath, ".git", "objects"), internalDatabase);
        WriteAlternate(internalDatabase, externalDatabase);
        Directory.CreateDirectory(externalDatabase);
    }

    public void ConfigureNestedAllowedAlternates()
    {
        var firstDatabase = Path.Combine(RepositoryPath, "allowed-objects-one");
        var secondDatabase = Path.Combine(RepositoryPath, "allowed-objects-two");
        WriteAlternate(Path.Combine(RepositoryPath, ".git", "objects"), firstDatabase);
        WriteAlternate(firstDatabase, secondDatabase);
        Directory.CreateDirectory(secondDatabase);
    }

    private void RewriteWorktreeMetadata(string worktreePath, string gitDirectory)
    {
        var gitFile = Path.Combine(worktreePath, ".git");
        File.SetAttributes(gitFile, FileAttributes.Normal);
        File.WriteAllText(gitFile, $"gitdir: {gitDirectory}\n");
        File.WriteAllText(
            Path.Combine(gitDirectory, "commondir"),
            Path.Combine(RepositoryPath, ".git"));
    }

    private static void WriteAlternate(string objectDatabase, string alternate)
    {
        var informationDirectory = Path.Combine(objectDatabase, "info");
        Directory.CreateDirectory(informationDirectory);
        File.WriteAllText(Path.Combine(informationDirectory, "alternates"), alternate);
    }
}
