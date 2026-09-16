using App.GitHealth.Api.Tests.Hosting;

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
        // No pool to clear here: every run built from this directory is a factory of its own,
        // and each one releases its handles as it is disposed — before this, since the
        // directory outlives them.
        if (Directory.Exists(_path))
        {
            Directory.Delete(_path, recursive: true);
        }
    }
}
