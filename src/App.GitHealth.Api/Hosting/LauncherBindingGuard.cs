namespace App.GitHealth.Api.Hosting;

internal static class LauncherBindingGuard
{
    private const string KestrelEndpointsSection = "Kestrel:Endpoints";

    public static bool HasConfiguredKestrelEndpoints(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration
            .GetSection(KestrelEndpointsSection)
            .GetChildren()
            .Any();
    }
}
