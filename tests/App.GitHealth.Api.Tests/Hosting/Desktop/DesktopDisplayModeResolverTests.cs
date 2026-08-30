using App.GitHealth.Api.Hosting;
using App.GitHealth.Api.Hosting.Desktop;

namespace App.GitHealth.Api.Tests.Hosting.Desktop;

public sealed class DesktopDisplayModeResolverTests
{
    [Fact]
    public void DefaultLaunchOpensTheDesktopWindow()
    {
        var options = LauncherOptionsParser.Parse([]).Options!;

        Assert.Equal(DesktopDisplayMode.Window, DesktopDisplayModeResolver.Resolve(options));
    }

    [Fact]
    public void NoWindowFallsBackToTheSystemBrowser()
    {
        var options = LauncherOptionsParser.Parse(["--no-window"]).Options!;

        Assert.Equal(
            DesktopDisplayMode.SystemBrowser,
            DesktopDisplayModeResolver.Resolve(options));
    }

    /// <summary>Le smoke test natif passe ce drapeau : une fenêtre y bloquerait la CI.</summary>
    [Fact]
    public void NoBrowserOpensNoInterfaceEvenWithoutNoWindow()
    {
        var options = LauncherOptionsParser.Parse(["--no-browser"]).Options!;

        Assert.True(options.ShouldOpenWindow);
        Assert.Equal(DesktopDisplayMode.None, DesktopDisplayModeResolver.Resolve(options));
    }

    [Fact]
    public void BothFlagsTogetherStillOpenNoInterface()
    {
        var options = LauncherOptionsParser.Parse(["--no-window", "--no-browser"]).Options!;

        Assert.Equal(DesktopDisplayMode.None, DesktopDisplayModeResolver.Resolve(options));
    }

    [Fact]
    public void NoWindowIsRejectedTwiceOrWithAValue()
    {
        Assert.False(LauncherOptionsParser.Parse(["--no-window", "--no-window"]).IsSuccess);
        Assert.False(LauncherOptionsParser.Parse(["--no-window=true"]).IsSuccess);
    }
}
