using System.Globalization;
using App.GitHealth.Api.Tests.Security;
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
    private const int DefaultAnalysisTimeoutSeconds = 300;
    private const int DefaultParallelAnalyses = 4;
    private readonly string _privateDirectory = Path.Combine(
        Path.GetTempPath(),
        "GitHealth-api-tests",
        Guid.NewGuid().ToString("N"));
    private IHost? _host;

    /// <summary>
    /// Data directory shared with another run of the application, and left to the caller to
    /// delete. Unset, the factory gets one of its own and removes it: only a test that has to
    /// observe what survives a restart needs two runs reading the same directory.
    /// </summary>
    /// <remarks>
    /// An <c>init</c> property rather than a constructor parameter, deliberately: xUnit
    /// refuses a class fixture that declares more than one public constructor, and most tests
    /// here take this factory as one.
    /// </remarks>
    public string? SharedDataDirectory { get; init; }

    public string DatabasePath => Path.Combine(
        SharedDataDirectory ?? _privateDirectory,
        "githealth.db");

    public string? InitialRepositoryPath { get; init; }

    public string? RepositoriesRoot { get; init; }

    public int QueueCapacity { get; init; } = DefaultQueueCapacity;

    public int AnalysisTimeoutSeconds { get; init; } = DefaultAnalysisTimeoutSeconds;

    public int MaximumParallelAnalyses { get; init; } = DefaultParallelAnalyses;

    /// <summary>Mirrors the installation-wide switch of the local agent assistant.</summary>
    public bool AssistantEnabled { get; init; } = true;

    public Action<IServiceCollection>? TestServices { get; init; }

    public Task StopHostAsync(CancellationToken cancellationToken) =>
        _host?.StopAsync(cancellationToken) ?? Task.CompletedTask;

    public new HttpClient CreateClient() =>
        CreateDefaultClient(new LocalSessionHandler());

    public HttpClient CreateRawClient() => base.CreateClient();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:DatabasePath"] = DatabasePath,
                ["GitHealth:InitialRepositoryPath"] = InitialRepositoryPath,
                ["GitHealth:RepositoriesRoot"] = RepositoriesRoot,
                ["AnalysisQueue:Capacity"] = QueueCapacity.ToString(
                    CultureInfo.InvariantCulture),
                ["AnalysisQueue:TimeoutSeconds"] = AnalysisTimeoutSeconds.ToString(
                    CultureInfo.InvariantCulture),
                ["AnalysisQueue:MaximumParallelAnalyses"] = MaximumParallelAnalyses.ToString(
                    CultureInfo.InvariantCulture),
                ["GitHealth:Assistant:Enabled"] = AssistantEnabled.ToString(
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
        if (SharedDataDirectory is null && Directory.Exists(_privateDirectory))
        {
            Directory.Delete(_privateDirectory, recursive: true);
        }
    }
}
