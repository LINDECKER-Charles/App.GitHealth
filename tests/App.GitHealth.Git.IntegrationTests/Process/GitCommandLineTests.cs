using App.GitHealth.Api.Git.Process;

namespace App.GitHealth.Git.IntegrationTests.Process;

public sealed class GitCommandLineTests
{
    [Fact]
    public void DescribeDropsTheRepositoryTheCommandIsAlreadyKnownToTarget()
    {
        var command = GitCommand.CreateRepository(
            Environment.CurrentDirectory,
            ["-C", Environment.CurrentDirectory, "merge-base", "main", "dev"]);

        Assert.Equal("git merge-base main dev", GitCommandLine.Describe(command));
    }

    [Fact]
    public void DescribeKeepsACommandThatTargetsNoRepository()
    {
        var command = GitCommand.Create(Environment.CurrentDirectory, ["--version"]);

        Assert.Equal("git --version", GitCommandLine.Describe(command));
    }

    [Fact]
    public void DescribeQuotesAnArgumentThatCarriesASpace()
    {
        var command = GitCommand.Create(Environment.CurrentDirectory, ["log", "--format=%aN %aE"]);

        Assert.Equal("git log '--format=%aN %aE'", GitCommandLine.Describe(command));
    }

    [Fact]
    public void DescribeShortensACommandTooLongToRead()
    {
        var command = GitCommand.Create(Environment.CurrentDirectory, [new string('a', 400)]);

        var described = GitCommandLine.Describe(command);

        Assert.Equal(220, described.Length);
        Assert.EndsWith("…", described, StringComparison.Ordinal);
    }

    [Fact]
    public void SummariseKeepsTheFirstLineOnly()
    {
        Assert.Equal("c480b1a7", GitCommandLine.SummariseOutput("c480b1a7\ne1637d57\n"));
        Assert.Equal("2 Ada Lovelace", GitCommandLine.SummariseOutput("\n\t2\tAda Lovelace\n"));
    }

    [Fact]
    public void SummariseReturnsNothingWhenGitAnsweredNothing()
    {
        Assert.Null(GitCommandLine.SummariseOutput(string.Empty));
        Assert.Null(GitCommandLine.SummariseOutput("   \n \n"));
    }
}
