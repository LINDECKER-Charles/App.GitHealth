namespace App.GitHealth.Api.Persistence.Services;

internal sealed class DatabaseMigrationHostedService(
    IDatabaseMigrationService migrationService,
    DatabaseInstanceLease instanceLease) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        instanceLease.Acquire();
        try
        {
            await migrationService.InitializeAsync(cancellationToken);
        }
        catch
        {
            instanceLease.Dispose();
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        instanceLease.Dispose();
        return Task.CompletedTask;
    }
}
