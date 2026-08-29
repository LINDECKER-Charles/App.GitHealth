using System.Net;
using App.GitHealth.Api.Hosting;

namespace App.GitHealth.Api.Tests.Hosting;

public sealed class LauncherOptionsParserTests
{
    [Fact]
    public void ParseUsesAutomaticLoopbackDefaults()
    {
        var result = LauncherOptionsParser.Parse([]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<LauncherOptions>(result.Options);
        Assert.Equal(LauncherOptions.AutomaticPort, options.Port);
        Assert.Equal(IPAddress.Loopback, LauncherOptions.ListenAddress);
        Assert.True(options.ShouldOpenBrowser);
        Assert.False(options.ShowHelp);
        Assert.Null(options.RepositoryPath);
        Assert.Null(options.DataDirectory);
        Assert.Empty(options.HostArguments);
        Assert.Equal(
            "http://127.0.0.1:5187/",
            LauncherOptions.CreateApplicationAddress(5187).AbsoluteUri);
    }

    [Fact]
    public void ParseReadsLauncherOptionsAndPreservesHostArguments()
    {
        var arguments = new[]
        {
            "--repo", "D:/dépôts/produit",
            "--environment", "Development",
            "--port=5187",
            "--data-dir", "D:/données GitHealth",
            "--no-browser",
        };

        var result = LauncherOptionsParser.Parse(arguments);

        var options = Assert.IsType<LauncherOptions>(result.Options);
        Assert.Equal("D:/dépôts/produit", options.RepositoryPath);
        Assert.Equal(5187, options.Port);
        Assert.Equal("D:/données GitHealth", options.DataDirectory);
        Assert.False(options.ShouldOpenBrowser);
        Assert.Equal(["--environment", "Development"], options.HostArguments);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void ParseRecognizesHelpWithoutForwardingIt(string argument)
    {
        var result = LauncherOptionsParser.Parse([argument]);

        var options = Assert.IsType<LauncherOptions>(result.Options);
        Assert.True(options.ShowHelp);
        Assert.Empty(options.HostArguments);
    }

    [Theory]
    [InlineData("--repo=")]
    [InlineData("--data-dir=")]
    [InlineData("--port")]
    [InlineData("--port=0")]
    [InlineData("--port=65536")]
    [InlineData("--port=abc")]
    [InlineData("--no-browser=true")]
    [InlineData("--help=true")]
    public void ParseRejectsInvalidLauncherArguments(string argument)
    {
        var result = LauncherOptionsParser.Parse([argument]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Options);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void ParseRejectsMissingValueBeforeAnotherOption()
    {
        var result = LauncherOptionsParser.Parse(["--repo", "--port", "5187"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("--repo", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseRejectsDuplicateLauncherOption()
    {
        var result = LauncherOptionsParser.Parse(["--port", "5187", "--port=5188"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("qu’une fois", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationAddressRejectsAnUnboundPort()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LauncherOptions.CreateApplicationAddress(0));
    }
}
