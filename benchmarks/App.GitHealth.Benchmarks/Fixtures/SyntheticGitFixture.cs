using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace App.GitHealth.Benchmarks.Fixtures;

internal sealed class SyntheticGitFixture : IAsyncDisposable
{
    private const long BaseTimestamp = 1_700_000_000;
    private readonly string _fixtureRoot;

    private SyntheticGitFixture(string fixtureRoot, string repositoryPath, string fingerprint)
    {
        _fixtureRoot = fixtureRoot;
        RepositoryPath = repositoryPath;
        Fingerprint = fingerprint;
    }

    public string RepositoryPath { get; }

    public string Fingerprint { get; }

    public static async Task<SyntheticGitFixture> CreateAsync(
        int branchCount,
        CancellationToken cancellationToken)
    {
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "githealth-benchmarks",
            Guid.NewGuid().ToString("N"));
        var repositoryPath = Path.Combine(fixtureRoot, "repository");
        Directory.CreateDirectory(repositoryPath);

        try
        {
            await InitializeRepositoryAsync(repositoryPath, branchCount, cancellationToken);
            var fingerprint = await ComputeFingerprintAsync(
                repositoryPath,
                cancellationToken);
            return new SyntheticGitFixture(fixtureRoot, repositoryPath, fingerprint);
        }
        catch
        {
            DeleteFixture(fixtureRoot);
            throw;
        }
    }

    private static async Task InitializeRepositoryAsync(
        string repositoryPath,
        int branchCount,
        CancellationToken cancellationToken)
    {
        await GitCommandExecutor.RunAsync(
            repositoryPath,
            ["init", "--initial-branch=main", "--quiet"],
            cancellationToken);
        await GitCommandExecutor.RunAsync(
            repositoryPath,
            ["config", "core.autocrlf", "false"],
            cancellationToken);
        var import = new GitCommandRequest(
            repositoryPath,
            ["fast-import", "--quiet"],
            BuildFastImport(branchCount));
        await GitCommandExecutor.RunAsync(import, cancellationToken);
        await GitCommandExecutor.RunAsync(
            repositoryPath,
            ["reset", "--hard", "--quiet", "main"],
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(repositoryPath, ".mailmap"),
            "Benchmark User <benchmark@example.test> Alias <alias@example.test>\n",
            Encoding.UTF8,
            cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        DeleteFixture(_fixtureRoot);
        return ValueTask.CompletedTask;
    }

    private static string BuildFastImport(int branchCount)
    {
        var stream = new StringBuilder(capacity: branchCount * 300);
        AppendBase(stream);
        for (var branch = 1; branch <= branchCount; branch++)
        {
            AppendBranch(stream, branch);
        }

        stream.Append("done\n");
        return stream.ToString();
    }

    private static void AppendBase(StringBuilder stream)
    {
        AppendBlob(stream, mark: 1, "benchmark base\n");
        AppendCommitHeader(
            stream,
            new FastImportCommit
            {
                Reference = "refs/heads/main",
                Mark = 2,
                Timestamp = BaseTimestamp,
                Message = "base",
            });
        stream.Append("M 100644 :1 benchmark-base.txt\n\n");
    }

    private static void AppendBranch(StringBuilder stream, int branch)
    {
        AppendCommitHeader(
            stream,
            new FastImportCommit
            {
                Reference = $"refs/remotes/origin/benchmark/{branch:0000}",
                Mark = branch + 2,
                Timestamp = BaseTimestamp + branch,
                Message = $"branch {branch:0000}",
            });
        stream.Append("from :2\n");
        stream.Append(
            CultureInfo.InvariantCulture,
            $"M 100644 inline branch-{branch:0000}.txt\n");
        AppendData(stream, $"content {branch:0000}\n");
    }

    private static void AppendBlob(StringBuilder stream, int mark, string content)
    {
        stream.Append("blob\n");
        stream.Append(CultureInfo.InvariantCulture, $"mark :{mark}\n");
        AppendData(stream, content);
    }

    private static void AppendCommitHeader(
        StringBuilder stream,
        FastImportCommit commit)
    {
        stream.Append(CultureInfo.InvariantCulture, $"commit {commit.Reference}\n");
        stream.Append(CultureInfo.InvariantCulture, $"mark :{commit.Mark}\n");
        stream.Append(
            CultureInfo.InvariantCulture,
            $"author Alias <alias@example.test> {commit.Timestamp} +0000\n");
        stream.Append(
            CultureInfo.InvariantCulture,
            $"committer Benchmark <benchmark@example.test> {commit.Timestamp} +0000\n");
        AppendData(stream, commit.Message);
    }

    private static void AppendData(StringBuilder stream, string content)
    {
        stream.Append(
            CultureInfo.InvariantCulture,
            $"data {Encoding.UTF8.GetByteCount(content)}\n");
        stream.Append(content);
        stream.Append('\n');
    }

    private static async Task<string> ComputeFingerprintAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var references = await GitCommandExecutor.RunAsync(
            repositoryPath,
            ["for-each-ref", "--sort=refname", "--format=%(refname):%(objectname)"],
            cancellationToken);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(references));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static void DeleteFixture(string fixtureRoot)
    {
        if (!Directory.Exists(fixtureRoot))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(
                     fixtureRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(fixtureRoot, recursive: true);
    }

    private sealed record FastImportCommit
    {
        public required string Reference { get; init; }

        public required int Mark { get; init; }

        public required long Timestamp { get; init; }

        public required string Message { get; init; }
    }
}
