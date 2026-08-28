using App.GitHealth.Api.Git.Models;
using App.GitHealth.Api.Git.Parsing;
using App.GitHealth.Api.Git.Process;
using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Api.Git.Scanning;

internal static class GitRepositoryReader
{
    private const string ReferenceFormat =
        "%(refname)%00%(objectname)%00%(symref)%00%(committerdate:unix)%00" +
        "%(authorname:mailmap)%00";

    public static async Task<CapturedRepository> CaptureAsync(
        IGitProcessRunner runner,
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(repositoryPath))
        {
            throw new GitProcessException(
                RepositoryErrorCode.PathNotFound,
                "Le chemin du dépôt n’existe pas.");
        }

        var context = await LocateAsync(runner, repositoryPath, cancellationToken);
        var version = await ReadVersionAsync(runner, cancellationToken);
        var references = await ReadReferencesAsync(runner, context, cancellationToken);
        return new CapturedRepository(context, version, references);
    }

    private static async Task<GitRepositoryContext> LocateAsync(
        IGitProcessRunner runner,
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(repositoryPath);
        var result = await RunAsync(
            runner,
            ["-C", fullPath, "rev-parse", "--absolute-git-dir", "--is-bare-repository"],
            cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new GitProcessException(
                RepositoryErrorCode.NotARepository,
                "Le chemin ne correspond pas à un dépôt Git lisible.");
        }

        var (gitDirectory, isBare) = ParseLocation(result.StandardOutput);
        if (isBare)
        {
            return new GitRepositoryContext(fullPath, gitDirectory, null);
        }

        var worktree = await ReadWorkingTreeAsync(runner, fullPath, cancellationToken);
        return new GitRepositoryContext(fullPath, gitDirectory, worktree);
    }

    private static (string GitDirectory, bool IsBare) ParseLocation(string output)
    {
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length != 2 || !bool.TryParse(lines[1], out var isBare))
        {
            throw new GitProcessException(
                RepositoryErrorCode.MalformedOutput,
                "Git a retourné une localisation de dépôt invalide.");
        }

        return (Path.GetFullPath(lines[0]), isBare);
    }

    private static async Task<string> ReadWorkingTreeAsync(
        IGitProcessRunner runner,
        string fullPath,
        CancellationToken cancellationToken)
    {
        var rootResult = await RunAsync(
            runner,
            ["-C", fullPath, "rev-parse", "--show-toplevel"],
            cancellationToken);
        EnsureSuccess(rootResult, "Impossible de déterminer le worktree Git.");
        return Path.GetFullPath(rootResult.StandardOutput.Trim());
    }

    private static async Task<string> ReadVersionAsync(
        IGitProcessRunner runner,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(runner, ["--version"], cancellationToken);
        EnsureSuccess(result, "Git ne répond pas à la détection de version.");
        return result.StandardOutput.Trim();
    }

    private static async Task<IReadOnlyDictionary<string, CapturedReference>>
        ReadReferencesAsync(
            IGitProcessRunner runner,
            GitRepositoryContext context,
            CancellationToken cancellationToken)
    {
        var arguments = new[]
        {
            "-C",
            context.InvocationPath,
            "for-each-ref",
            "--sort=refname",
            $"--format={ReferenceFormat}",
            "refs/heads",
            "refs/remotes",
        };
        var result = await RunAsync(runner, arguments, cancellationToken);
        EnsureSuccess(result, "Impossible de lire les références Git.");
        return GitOutputParser.ParseReferences(result.StandardOutput);
    }

    private static Task<GitCommandResult> RunAsync(
        IGitProcessRunner runner,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        var command = GitCommand.Create(Environment.CurrentDirectory, arguments);
        return runner.RunAsync(command, cancellationToken);
    }

    private static void EnsureSuccess(GitCommandResult result, string message)
    {
        if (result.ExitCode != 0)
        {
            throw new GitProcessException(RepositoryErrorCode.ProcessFailed, message);
        }
    }
}
