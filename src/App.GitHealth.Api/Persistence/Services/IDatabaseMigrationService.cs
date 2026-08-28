namespace App.GitHealth.Api.Persistence.Services;

internal interface IDatabaseMigrationService
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
