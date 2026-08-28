namespace App.GitHealth.Api.Persistence.Services;

internal sealed class DatabaseMigrationHostedService(
    IDatabaseMigrationService migrationService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        migrationService.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
