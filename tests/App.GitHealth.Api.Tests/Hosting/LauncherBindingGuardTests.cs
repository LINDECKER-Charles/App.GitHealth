using App.GitHealth.Api.Hosting;
using Microsoft.Extensions.Configuration;

namespace App.GitHealth.Api.Tests.Hosting;

public sealed class LauncherBindingGuardTests
{
    [Fact]
    public void DetectsConfiguredKestrelEndpoints()
    {
        var configuration = Configuration(
            "Kestrel:Endpoints:Public:Url",
            "http://0.0.0.0:8080");

        Assert.True(LauncherBindingGuard.HasConfiguredKestrelEndpoints(configuration));
    }

    [Fact]
    public void AllowsUnrelatedKestrelSettings()
    {
        var configuration = Configuration(
            "Kestrel:Limits:MaxRequestBodySize",
            "131072");

        Assert.False(LauncherBindingGuard.HasConfiguredKestrelEndpoints(configuration));
    }

    private static IConfiguration Configuration(string key, string value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [key] = value,
            })
            .Build();
}
