using System.Diagnostics;

namespace App.GitHealth.Api.Tests.Discovery;

/// <summary>Arborescence temporaire où poser des dépôts Git à des profondeurs choisies.</summary>
public sealed class DiscoveryWorkspace : IDisposable
{
    private const int GitTimeoutMilliseconds = 10_000;
    private const string MainBranch = "main";

    private DiscoveryWorkspace(string rootPath)
    {
        RootPath = rootPath;
    }

    public string RootPath { get; }

    public static DiscoveryWorkspace Create()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "GitHealth-discovery-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        return new DiscoveryWorkspace(rootPath);
    }

    public string AddDirectory(string relativePath)
    {
        var path = Resolve(relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Dépôt standard avec une branche <c>main</c> et un commit, donc lisible.</summary>
    public string AddRepository(string relativePath)
    {
        var path = AddDirectory(relativePath);
        Run(path, "init");
        Run(path, "config", "user.name", "Git Health Tests");
        Run(path, "config", "user.email", "git-health@example.test");
        Run(path, "checkout", "-b", MainBranch);
        File.WriteAllText(Path.Combine(path, "initial.txt"), relativePath);
        Run(path, "add", "initial.txt");
        Run(path, "commit", "-m", "initial commit");
        return path;
    }

    public string Resolve(string relativePath) =>
        Path.GetFullPath(Path.Combine(RootPath, relativePath));

    public void Dispose()
    {
        if (!Directory.Exists(RootPath))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(RootPath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        }

        Directory.Delete(RootPath, recursive: true);
    }

    private static void Run(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Impossible de lancer Git.");
        var standardError = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(GitTimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("La préparation du dépôt Git a expiré.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Git a échoué : {standardError}");
        }
    }
}
