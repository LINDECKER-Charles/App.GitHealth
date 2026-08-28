using App.GitHealth.Api.Git.Parsing;
using App.GitHealth.Api.Git.Process;
using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Git.IntegrationTests.Parsing;

public sealed class GitOutputParserTests
{
    [Theory]
    [InlineData("refs/heads/main\0not-hex\0\01700000000\0Ada\0\n")]
    [InlineData("refs/heads/main\0abcdef\0\0not-a-date\0Ada\0\n")]
    [InlineData("refs/heads/main\0abcdef\0")]
    public void MalformedReferenceOutputProducesAControlledError(string output)
    {
        var exception = Assert.Throws<GitProcessException>(() =>
            GitOutputParser.ParseReferences(output));

        Assert.Equal(RepositoryErrorCode.MalformedOutput, exception.Code);
    }
}
