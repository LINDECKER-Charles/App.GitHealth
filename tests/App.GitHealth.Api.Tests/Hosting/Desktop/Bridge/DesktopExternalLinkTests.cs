using System.ComponentModel;
using System.Diagnostics;
using App.GitHealth.Api.Hosting;
using App.GitHealth.Api.Hosting.Desktop.Bridge;

namespace App.GitHealth.Api.Tests.Hosting.Desktop.Bridge;

public sealed class DesktopExternalLinkTests
{
    [Fact]
    public void OpenHandsAnAbsoluteWebAddressToTheBrowser()
    {
        ProcessStartInfo? captured = null;
        var launcher = new SystemBrowserLauncher(startInfo => captured = startInfo);

        var isHandled = DesktopExternalLink.Open("https://example.test/guide", launcher);

        Assert.True(isHandled);
        Assert.Equal("https://example.test/guide", captured?.FileName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/docs/USER_GUIDE.md")]
    public void OpenTurnsAwayAnAddressThatIsNotAbsolute(string? url)
    {
        var wasStarted = false;
        var launcher = new SystemBrowserLauncher(_ => wasStarted = true);

        var isHandled = DesktopExternalLink.Open(url, launcher);

        Assert.False(isHandled);
        Assert.False(wasStarted);
    }

    [Fact]
    public void OpenTurnsAwayASchemeThatIsNotTheWeb()
    {
        var wasStarted = false;
        var launcher = new SystemBrowserLauncher(_ => wasStarted = true);

        var isHandled = DesktopExternalLink.Open("file:///etc/passwd", launcher);

        Assert.False(isHandled);
        Assert.False(wasStarted);
    }

    [Fact]
    public void OpenReportsTheFailureWhenTheSystemRefusesTheBrowser()
    {
        var launcher = new SystemBrowserLauncher(_ => throw new Win32Exception("No browser"));

        Assert.False(DesktopExternalLink.Open("https://example.test/guide", launcher));
    }

    [Fact]
    public void HandleAnswersOnTheKindAndIdItWasAsked()
    {
        var reply = DesktopExternalLink.Handle("not an address", "7");

        Assert.Equal("7", reply.Id);
        Assert.Equal("openExternal", reply.Kind);
        Assert.False(reply.IsHandled);
    }
}
