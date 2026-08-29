using System.ComponentModel;
using System.Diagnostics;
using App.GitHealth.Api.Hosting;

namespace App.GitHealth.Api.Tests.Hosting;

public sealed class SystemBrowserLauncherTests
{
    [Fact]
    public void OpenStartsTheDefaultBrowserWithoutWaiting()
    {
        ProcessStartInfo? captured = null;
        var launcher = new SystemBrowserLauncher(startInfo => captured = startInfo);
        var address = new Uri("http://127.0.0.1:5187/");

        var warning = launcher.Open(address);

        Assert.Null(warning);
        Assert.NotNull(captured);
        Assert.Equal(address.AbsoluteUri, captured.FileName);
        Assert.True(captured.UseShellExecute);
    }

    [Fact]
    public void OpenReturnsManualInstructionWhenTheSystemRefusesTheBrowser()
    {
        var launcher = new SystemBrowserLauncher(
            _ => throw new Win32Exception("Aucun navigateur"));
        var address = new Uri("http://127.0.0.1:5187/");

        var warning = launcher.Open(address);

        Assert.Contains(address.AbsoluteUri, warning, StringComparison.Ordinal);
        Assert.Contains("manuellement", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenRejectsNonHttpAddressWithoutStartingAProcess()
    {
        var wasStarted = false;
        var launcher = new SystemBrowserLauncher(_ => wasStarted = true);

        var warning = launcher.Open(new Uri("file:///tmp/githealth"));

        Assert.False(wasStarted);
        Assert.Contains("ne peut pas être ouverte", warning, StringComparison.Ordinal);
    }
}
