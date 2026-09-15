using App.GitHealth.Api.Tests.Hosting;
using Microsoft.Data.Sqlite;

namespace App.GitHealth.Api.Tests.LocalApi;

/// <summary>
/// One data directory, several successive runs of the application. Restarting is the only way
/// to observe what a setting written to disk actually survives.
/// </summary>
internal sealed class ScopedDataDirectory : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        "GitHealth-local-api-tests",
        Guid.NewGuid().ToString("N"));

    public ApiApplicationFactory CreateFactory() => new() { SharedDataDirectory = _path };

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_path))
        {
            Directory.Delete(_path, recursive: true);
        }
    }
}
