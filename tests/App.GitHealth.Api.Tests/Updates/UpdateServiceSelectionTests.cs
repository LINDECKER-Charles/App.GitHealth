using App.GitHealth.Api.Features.Updates;
using App.GitHealth.Api.Hosting;

namespace App.GitHealth.Api.Tests.Updates;

public sealed class UpdateServiceSelectionTests
{
    [Fact]
    public void NativeWindowsAndMacOsSupportInAppUpdates()
    {
        Assert.True(UpdateServiceCollectionExtensions.SupportsInAppUpdates(
            useNativeLauncher: true,
            RuntimePlatform.Windows));
        Assert.True(UpdateServiceCollectionExtensions.SupportsInAppUpdates(
            useNativeLauncher: true,
            RuntimePlatform.MacOS));
    }

    /// <summary>Un utilisateur Linux attend son gestionnaire de paquets, pas un bouton.</summary>
    [Fact]
    public void LinuxNeverSupportsInAppUpdates()
    {
        Assert.False(UpdateServiceCollectionExtensions.SupportsInAppUpdates(
            useNativeLauncher: true,
            RuntimePlatform.Linux));
    }

    /// <summary>
    /// Hors lanceur natif — Docker, navigateur — il n'y a rien à mettre à jour.
    /// </summary>
    [Fact]
    public void NonNativeLaunchNeverSupportsInAppUpdates()
    {
        Assert.False(UpdateServiceCollectionExtensions.SupportsInAppUpdates(
            useNativeLauncher: false,
            RuntimePlatform.Windows));
        Assert.False(UpdateServiceCollectionExtensions.SupportsInAppUpdates(
            useNativeLauncher: false,
            RuntimePlatform.MacOS));
        Assert.False(UpdateServiceCollectionExtensions.SupportsInAppUpdates(
            useNativeLauncher: false,
            RuntimePlatform.Linux));
    }

    [Fact]
    public async Task DefaultServiceReportsThatUpdatesAreUnsupported()
    {
        var service = new NullUpdateService();

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.Equal(nameof(UpdateAvailability.Unsupported), status.Availability);
        Assert.Null(status.CurrentVersion);
        Assert.Null(status.AvailableVersion);
        Assert.False(await service.DownloadAsync(CancellationToken.None));
        Assert.Throws<NotSupportedException>(service.ApplyAndRestart);
    }
}
