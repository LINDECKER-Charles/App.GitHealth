using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace App.GitHealth.Api.Tests.Hosting;

public sealed class ApiApplicationFactory : WebApplicationFactory<Program>
{
    private const int DefaultQueueCapacity = 32;
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "GitHealth-api-tests",
        Guid.NewGuid().ToString("N"));
    private IHost? _host;

    public string DatabasePath => Path.Combine(_directory, "githealth.db");

    public string? RepositoriesRoot { get; init; }

    public int QueueCapacity { get; init; } = DefaultQueueCapacity;

    public Action<IServiceCollection>? TestServices { get; init; }

    public Task StopHostAsync(CancellationToken cancellationToken) =>
        _host?.StopAsync(cancellationToken) ?? Task.CompletedTask;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:DatabasePath"] = DatabasePath,
                ["GitHealth:RepositoriesRoot"] = RepositoriesRoot,
                ["AnalysisQueue:Capacity"] = QueueCapacity.ToString(
                    CultureInfo.InvariantCulture),
            });
        });
        if (TestServices is not null)
        {
            builder.ConfigureTestServices(TestServices);
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        _host = base.CreateHost(builder);
        return _host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
