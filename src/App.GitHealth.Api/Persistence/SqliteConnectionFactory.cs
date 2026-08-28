using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Persistence;

internal sealed class SqliteConnectionFactory
{
    public SqliteConnectionFactory(
        IOptions<PersistenceOptions> options,
        IHostEnvironment environment)
    {
        var settings = options.Value;
        DatabasePath = ResolvePath(settings.DatabasePath, environment.ContentRootPath);
        WriteTimeoutSeconds = settings.WriteTimeoutSeconds;
        ConnectionString = BuildConnectionString(DatabasePath, WriteTimeoutSeconds);
    }

    public string ConnectionString { get; }

    public string DatabasePath { get; }

    public int WriteTimeoutSeconds { get; }

    public SqliteConnection CreateConnection() => new(ConnectionString);

    private static string ResolvePath(string configuredPath, string contentRoot)
    {
        var path = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(contentRoot, configuredPath);
        return Path.GetFullPath(path);
    }

    private static string BuildConnectionString(string path, int timeoutSeconds)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
            ForeignKeys = true,
            DefaultTimeout = timeoutSeconds,
            Pooling = true,
        }.ToString();
    }
}
