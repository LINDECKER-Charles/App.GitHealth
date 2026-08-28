namespace App.GitHealth.Api.Persistence.Services;

internal interface IDatabaseBackupService
{
    Task ExportAsync(Stream destination, CancellationToken cancellationToken);
}
