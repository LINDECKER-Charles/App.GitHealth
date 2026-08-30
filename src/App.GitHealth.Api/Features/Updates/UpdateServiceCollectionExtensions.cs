using App.GitHealth.Api.Hosting;

namespace App.GitHealth.Api.Features.Updates;

/// <summary>
/// Choisit l'implémentation d'<see cref="IUpdateService" /> selon le mode d'exécution et
/// la plateforme.
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
    /// Les mises à jour in-app supposent une installation gérée : hors du lanceur natif
    /// il n'y en a pas, et sur Linux l'utilisateur attend son gestionnaire de paquets.
    /// </summary>
    public static bool SupportsInAppUpdates(bool useNativeLauncher, RuntimePlatform platform) =>
        useNativeLauncher && platform is RuntimePlatform.Windows or RuntimePlatform.MacOS;
}
