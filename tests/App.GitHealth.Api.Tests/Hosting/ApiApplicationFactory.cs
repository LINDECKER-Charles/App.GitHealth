using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace App.GitHealth.Api.Tests.Hosting;

public sealed class ApiApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "GitHealth-api-tests",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var databasePath = Path.Combine(_directory, "githealth.db");
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:DatabasePath"] = databasePath,
            });
        });
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
