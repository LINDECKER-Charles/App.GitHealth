using App.GitHealth.Api.Hosting;

namespace App.GitHealth.Api.Features.Updates;

/// <summary>
/// Chooses the <see cref="IUpdateService" /> implementation according to the run mode
/// and the platform.
/// </summary>
internal static class UpdateServiceCollectionExtensions
{
    public static IServiceCollection AddUpdates(
        this IServiceCollection services,
        bool useNativeLauncher)
    {
        ArgumentNullException.ThrowIfNull(services);
        var platform = LauncherEnvironment.Capture().Platform;
        if (SupportsInAppUpdates(useNativeLauncher, platform))
        {
            services.AddSingleton<IUpdateService, VelopackUpdateService>();
            return services;
        }

        services.AddSingleton<IUpdateService, NullUpdateService>();
        return services;
    }

    /// <summary>
    /// In-app updates assume a managed installation: outside the native launcher there is
    /// none, and on Linux the user expects their package manager.
    /// </summary>
    public static bool SupportsInAppUpdates(bool useNativeLauncher, RuntimePlatform platform) =>
        useNativeLauncher && platform is RuntimePlatform.Windows or RuntimePlatform.MacOS;
}
