using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Tests.Branches;

public sealed class CommitIdTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("0123456789abcdef0123456789abcdef01234567")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    public void ConstructorDoesNotAssumeAnObjectIdLength(string value)
    {
        var commit = new CommitId(value);

        Assert.Equal(value, commit.Value);
    }

    [Theory]
    [InlineData("not-hexadecimal")]
    [InlineData("0123 4567")]
    public void ConstructorRejectsNonHexadecimalValues(string value)
    {
        Assert.Throws<ArgumentException>(() => new CommitId(value));
    }
}
