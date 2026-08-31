using App.GitHealth.Api.Git.Models;
using App.GitHealth.Api.Git.Parsing;
using App.GitHealth.Api.Git.Paths;
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
        GitRepositoryCaptureRequest request,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(request.RepositoryPath))
        {
            throw new GitProcessException(
                RepositoryErrorCode.PathNotFound,
                "The repository path does not exist.");
        }

        EnsureInputAllowed(request.RepositoriesRoot, request.RepositoryPath);
        var context = await LocateAsync(runner, request.RepositoryPath, cancellationToken);
        RepositoryPathGuard.EnsureAllowed(request.RepositoriesRoot, context);
        await GitObjectDatabaseGuard.EnsureAllowedAsync(
            request.RepositoriesRoot,
            context.ObjectDirectory,
            cancellationToken);
        var version = await ReadVersionAsync(runner, cancellationToken);
        var references = await ReadReferencesAsync(runner, context, cancellationToken);
        return new CapturedRepository(context, version, references);
    }

    private static void EnsureInputAllowed(string? repositoriesRoot, string repositoryPath)
    {
        if (!RepositoryPathGuard.IsAllowed(repositoriesRoot, repositoryPath))
        {
            throw new GitProcessException(
                RepositoryErrorCode.PathNotAllowed,
                "The repository is outside the allowed root.");
        }
    }

    private static async Task<GitRepositoryContext> LocateAsync(
        IGitProcessRunner runner,
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(repositoryPath);
        var result = await RunAsync(
            runner,
            CreateLocationArguments(fullPath),
            fullPath,
            cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new GitProcessException(
                RepositoryErrorCode.NotARepository,
                "The path is not a readable Git repository.");
        }

        var (metadataPaths, isBare) = ParseLocation(result.StandardOutput);
        if (isBare)
        {
            return new GitRepositoryContext(fullPath, null, metadataPaths);
        }

        var worktree = await ReadWorkingTreeAsync(runner, fullPath, cancellationToken);
        return new GitRepositoryContext(fullPath, worktree, metadataPaths);
    }

    private static string[] CreateLocationArguments(string fullPath) =>
    [
        "-C",
        fullPath,
        "rev-parse",
        "--path-format=absolute",
        "--absolute-git-dir",
        "--git-common-dir",
        "--git-path",
        "objects",
        "--is-bare-repository",
    ];

    private static (GitRepositoryMetadataPaths MetadataPaths, bool IsBare) ParseLocation(
        string output)
    {
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length != 4 || !bool.TryParse(lines[3], out var isBare))
        {
            throw new GitProcessException(
                RepositoryErrorCode.MalformedOutput,
                "Git returned an invalid repository location.");
        }

        var metadataPaths = new GitRepositoryMetadataPaths(lines[0], lines[1], lines[2]);
        return (metadataPaths, isBare);
    }

    private static async Task<string> ReadWorkingTreeAsync(
        IGitProcessRunner runner,
        string fullPath,
        CancellationToken cancellationToken)
    {
        var rootResult = await RunAsync(
            runner,
            ["-C", fullPath, "rev-parse", "--show-toplevel"],
            fullPath,
            cancellationToken);
        EnsureSuccess(rootResult, "The Git worktree could not be determined.");
        return Path.GetFullPath(rootResult.StandardOutput.Trim());
    }

    private static async Task<string> ReadVersionAsync(
        IGitProcessRunner runner,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(runner, ["--version"], cancellationToken);
        EnsureSuccess(result, "Git does not answer the version probe.");
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
        var result = await RunAsync(
            runner,
            arguments,
            context.InvocationPath,
            cancellationToken);
        EnsureSuccess(result, "The Git references could not be read.");
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

    private static Task<GitCommandResult> RunAsync(
        IGitProcessRunner runner,
        IEnumerable<string> arguments,
        string safeDirectory,
        CancellationToken cancellationToken)
    {
        var command = GitCommand.CreateRepository(safeDirectory, arguments);
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
