using System.Globalization;
using App.GitHealth.Api.Persistence;
using App.GitHealth.Api.Persistence.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace App.GitHealth.Api.Tests.Persistence;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly bool _ownsDirectory;

    private SqliteTestDatabase(string rootPath, int? retentionDays, bool ownsDirectory)
    {
        RootPath = rootPath;
        _ownsDirectory = ownsDirectory;
        Services = BuildServices(rootPath, retentionDays);
    }

    public string RootPath { get; }

    public string DatabasePath => Path.Combine(RootPath, "githealth.db");

    public ServiceProvider Services { get; }

    public static async Task<SqliteTestDatabase> CreateAsync(int? retentionDays = null)
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "GitHealth-tests",
            Guid.NewGuid().ToString("N"));
        var database = new SqliteTestDatabase(rootPath, retentionDays, true);
        await database.InitializeAsync();
        return database;
    }

    public async Task<SqliteTestDatabase> ReopenAsync(int? retentionDays = null)
    {
        var database = new SqliteTestDatabase(RootPath, retentionDays, false);
        await database.InitializeAsync();
        return database;
    }

    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    public SqliteConnection CreateConnection() =>
        Services.GetRequiredService<SqliteConnectionFactory>().CreateConnection();

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (_ownsDirectory && Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, true);
        }
    }

    private async Task InitializeAsync()
    {
        var migration = Services.GetRequiredService<IDatabaseMigrationService>();
        await migration.InitializeAsync(CancellationToken.None);
    }

    private static ServiceProvider BuildServices(string rootPath, int? retentionDays)
    {
        var values = new Dictionary<string, string?>
        {
            [$"{PersistenceOptions.SectionName}:DatabasePath"] =
                Path.Combine(rootPath, "githealth.db"),
            [$"{PersistenceOptions.SectionName}:WriteTimeoutSeconds"] = "1",
            [$"{PersistenceOptions.SectionName}:RetentionDays"] =
                retentionDays?.ToString(CultureInfo.InvariantCulture),
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(rootPath));
        services.AddPersistence(configuration);
        return services.BuildServiceProvider(true);
    }
}
