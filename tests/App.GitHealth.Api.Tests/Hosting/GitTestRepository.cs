using System.Diagnostics;

namespace App.GitHealth.Api.Tests.Hosting;

public sealed class GitTestRepository : IDisposable
{
    private const int GitTimeoutMilliseconds = 10_000;
    private const string MainBranch = "main";

    private GitTestRepository(string rootPath)
    {
        RootPath = rootPath;
        RepositoryPath = Path.Combine(rootPath, "repository");
    }

    public string RootPath { get; }

    public string RepositoryPath { get; }

    public static GitTestRepository Create(int aheadBranchCount = 3)
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "GitHealth-api-repositories",
            Guid.NewGuid().ToString("N"));
        var repository = new GitTestRepository(rootPath);
        repository.Initialize(aheadBranchCount);
        return repository;
    }

    public static void CreateDirectoryLink(string linkPath, string targetPath)
    {
        if (OperatingSystem.IsWindows())
        {
            CreateWindowsJunction(linkPath, targetPath);
            return;
        }

        Directory.CreateSymbolicLink(linkPath, targetPath);
    }

    public void DeleteRepository()
    {
        if (Directory.Exists(RepositoryPath))
        {
            MakeFilesWritable(RepositoryPath);
            Directory.Delete(RepositoryPath, recursive: true);
        }
    }

    public void AddAheadBranchWithAuthor(string branchName, string authorName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);
        Run("checkout", "-b", branchName);
        var fileName = $"{branchName.Replace('/', '-')}.txt";
        File.WriteAllText(Path.Combine(RepositoryPath, fileName), branchName);
        Run("add", fileName);
        Run(
            "-c",
            $"user.name={authorName}",
            "-c",
            "user.email=formula@example.test",
            "commit",
            "-m",
            $"add {branchName}");
        Run("checkout", MainBranch);
    }

    public void AddSynchronizedBranch(string branchName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        Run("branch", branchName, MainBranch);
    }

    public void AddMainCommit()
    {
        CommitFile(
            "source-identity.txt",
            "source repository identity",
            "add source identity");
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            MakeFilesWritable(RootPath);
            Directory.Delete(RootPath, recursive: true);
        }
    }

    private void Initialize(int aheadBranchCount)
    {
        Directory.CreateDirectory(RepositoryPath);
        Run("init");
        Run("config", "user.name", "Git Health Tests");
        Run("config", "user.email", "git-health@example.test");
        Run("checkout", "-b", MainBranch);
        CommitFile("initial.txt", "initial", "initial commit");
        Run("branch", "feature/behind");
        CommitFile("main.txt", "main", "main update");
        for (var index = 0; index < aheadBranchCount; index++)
        {
            AddAheadBranch(index);
        }
    }

    private void AddAheadBranch(int index)
    {
        var branchName = $"feature/near-{index:D2}";
        Run("checkout", "-b", branchName);
        CommitFile($"feature-{index:D2}.txt", branchName, $"add {branchName}");
        Run("checkout", MainBranch);
    }

    private void CommitFile(string fileName, string content, string message)
    {
        File.WriteAllText(Path.Combine(RepositoryPath, fileName), content);
        Run("add", fileName);
        Run("commit", "-m", message);
    }

    private void Run(params string[] arguments)
    {
        var startInfo = CreateStartInfo(arguments);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Git could not be started.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(GitTimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Preparing the Git repository timed out.");
        }

        if (process.ExitCode != 0)
        {
            var output = $"{standardError}{standardOutput}";
            throw new InvalidOperationException($"Git failed: {output}");
        }
    }

    private ProcessStartInfo CreateStartInfo(IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = RepositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void MakeFilesWritable(string path)
    {
        foreach (var file in Directory.EnumerateFiles(
            path,
            "*",
            SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(file);
            File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
        }
    }

    private static void CreateWindowsJunction(string linkPath, string targetPath)
    {
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "/d", "/c", "mklink", "/J", linkPath, targetPath })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The test junction did not start.");
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Invalid test junction: {error}");
        }
    }
}
