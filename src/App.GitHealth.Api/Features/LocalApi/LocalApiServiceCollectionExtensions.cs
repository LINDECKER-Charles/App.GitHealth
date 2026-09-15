namespace App.GitHealth.Api.Features.LocalApi;

internal static class LocalApiServiceCollectionExtensions
{
    /// <summary>
    /// Must be called after persistence: the settings file is resolved from the database path,
    /// and the lifecycle that reopens the port has to queue up behind the migrations.
    /// </summary>
    public static IServiceCollection AddLocalApi(this IServiceCollection services)
    {
        services.AddSingleton<LocalApiSettingsStore>();
        services.AddSingleton<LocalApiHost>();
        services.AddSingleton<LocalApiService>();
        services.AddHostedService<LocalApiLifecycle>();
        return services;
    }
}
