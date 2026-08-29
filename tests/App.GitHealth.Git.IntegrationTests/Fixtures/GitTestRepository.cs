using System.Diagnostics;
using System.Security.Cryptography;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace App.GitHealth.Git.IntegrationTests.Fixtures;

internal sealed partial class GitTestRepository : IDisposable
{
    private readonly string _containerPath;
    private int _commitSequence;

    private GitTestRepository(string containerPath, string repositoryPath)
    {
        _containerPath = containerPath;
        RepositoryPath = repositoryPath;
    }

    public string RepositoryPath { get; }

    public static GitTestRepository Create()
    {
        var container = Path.Combine(
            Path.GetTempPath(),
            "githealth-tests",
            Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(container, "repository");
        Directory.CreateDirectory(repository);
        var fixture = new GitTestRepository(container, repository);
        fixture.Initialize();
        return fixture;
    }

    public GitRepositorySnapshot TakeSnapshot()
    {
        var references = RunGit(
            "for-each-ref",
            "--sort=refname",
            "--format=%(refname):%(objectname)");
        var status = RunGit("status", "--porcelain=v2", "--untracked-files=all");
        var indexPath = RunGit("rev-parse", "--git-path", "index").Trim();
        var absoluteIndex = Path.IsPathRooted(indexPath)
            ? indexPath
            : Path.Combine(RepositoryPath, indexPath);
        var indexHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(absoluteIndex)));
        return new GitRepositorySnapshot(references, status, indexHash);
    }

    public string CreateBareClone()
    {
        var path = Path.Combine(_containerPath, "bare.git");
        RunExternal(_containerPath, ["clone", "--bare", RepositoryPath, path]);
        return path;
    }

    public string CreateLinkedWorktree()
    {
        var path = Path.Combine(_containerPath, "linked-worktree");
        RunGit("worktree", "add", path, "feature/ahead");
        return path;
    }

    public void MoveMetadataOutsideRepository()
    {
        var metadata = Path.Combine(RepositoryPath, ".git");
        var externalMetadata = Path.Combine(_containerPath, "external-metadata");
        Directory.Move(metadata, externalMetadata);
        File.WriteAllText(metadata, $"gitdir: {externalMetadata}\n");
    }

    public string CreateRepositoryLink()
    {
        var path = Path.Combine(_containerPath, "repository-link");
        if (OperatingSystem.IsWindows())
        {
            CreateWindowsJunction(path);
        }
        else
        {
            Directory.CreateSymbolicLink(path, RepositoryPath);
        }

        return path;
    }

    public void Dispose()
    {
        var repositoryLink = Path.Combine(_containerPath, "repository-link");
        if (Directory.Exists(repositoryLink))
        {
            Directory.Delete(repositoryLink);
        }

        if (Directory.Exists(_containerPath))
        {
            foreach (var file in Directory.EnumerateFiles(
                _containerPath,
                "*",
                SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_containerPath, recursive: true);
        }
    }

    private void Initialize()
    {
        RunGit("init", "--initial-branch=main");
        RunGit("config", "user.name", "Fixture User");
        RunGit("config", "user.email", "fixture@example.test");
        File.WriteAllText(
            Path.Combine(RepositoryPath, ".mailmap"),
            "Ada Lovelace <ada@example.test> Alias One <alias1@example.test>\n" +
            "Ada Lovelace <ada@example.test> Alias Two <alias2@example.test>\n");
        CommitFile("base.txt", "base", "Créer la base");
        CreateMergedBranch();
        CreateDivergedBranch();
        CreateReferenceMergeBranch();
        RunGit("branch", "feature/éclair%29");
        CreateOrphanBranch();
        CreateAheadBranch();
        CreateRemoteReferences();
    }

    private void CreateMergedBranch()
    {
        RunGit("switch", "-c", "feature/merged");
        CommitFile("merged.txt", "merged", "Créer une branche fusionnée");
        RunGit("switch", "main");
        RunGit("merge", "--no-ff", "--no-edit", "feature/merged");
    }

    private void CreateDivergedBranch()
    {
        RunGit("switch", "-c", "feature/diverged");
        CommitFile("diverged.txt", "branch", "Créer la divergence");
        RunGit("switch", "main");
        CommitFile("main.txt", "main", "Avancer la référence");
    }

    private void CreateOrphanBranch()
    {
        RunGit("switch", "--orphan", "feature/orpheline");
        CommitFile("orphan.txt", "orphan", "Créer un historique indépendant");
        RunGit("switch", "main");
    }

    private void CreateReferenceMergeBranch()
    {
        RunGit("switch", "feature/diverged");
        RunGit("switch", "-c", "feature/merge-reference");
        RunGit("merge", "--no-ff", "--no-edit", "main");
        RunGit("switch", "main");
    }

    private void CreateAheadBranch()
    {
        RunGit("switch", "-c", "feature/ahead");
        CommitFile(
            "ahead-one.txt",
            "one",
            new GitCommitDetails(
                "Premier commit propre",
                "Alias One",
                "alias1@example.test"));
        CommitFile(
            "ahead-two.txt",
            "two",
            new GitCommitDetails(
                "Second commit propre",
                "Alias Two",
                "alias2@example.test"));
        RunGit("switch", "main");
    }

    private void CreateRemoteReferences()
    {
        var main = RunGit("rev-parse", "refs/heads/main").Trim();
        RunGit("update-ref", "refs/remotes/origin/main", main);
        RunGit("symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/main");
    }

    private void CommitFile(string fileName, string content, string message)
    {
        CommitFile(
            fileName,
            content,
            new GitCommitDetails(message, "Fixture User", "fixture@example.test"));
    }

    private void CommitFile(string fileName, string content, GitCommitDetails details)
    {
        File.WriteAllText(Path.Combine(RepositoryPath, fileName), content);
        RunGit("add", "--", fileName);
        var timestamp = DateTimeOffset.UnixEpoch.AddDays(++_commitSequence).ToString("O");
        RunGit(
            ["commit", "-m", details.Message],
            new Dictionary<string, string>
            {
                ["GIT_AUTHOR_NAME"] = details.AuthorName,
                ["GIT_AUTHOR_EMAIL"] = details.AuthorEmail,
                ["GIT_AUTHOR_DATE"] = timestamp,
                ["GIT_COMMITTER_DATE"] = timestamp,
            });
    }

    private string RunGit(params string[] arguments) => RunGit(arguments, null);

    private string RunGit(
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment)
    {
        return RunExternal(RepositoryPath, arguments, environment);
    }

    private static string RunExternal(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        using var process = DiagnosticsProcess.Start(
            CreateStartInfo(workingDirectory, arguments, environment))
            ?? throw new InvalidOperationException("Git n’a pas démarré dans la fixture.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Git fixture: {error}");
        }

        return output;
    }

    private static ProcessStartInfo CreateStartInfo(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var variable in environment ?? new Dictionary<string, string>())
        {
            startInfo.Environment[variable.Key] = variable.Value;
        }

        return startInfo;
    }

    private void CreateWindowsJunction(string path)
    {
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "/d", "/c", "mklink", "/J", path, RepositoryPath })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = DiagnosticsProcess.Start(startInfo)
            ?? throw new InvalidOperationException("La jonction de test n’a pas démarré.");
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Jonction de test invalide : {error}");
        }
    }
}
