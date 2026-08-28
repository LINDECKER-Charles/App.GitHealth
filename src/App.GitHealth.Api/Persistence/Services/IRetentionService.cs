using App.GitHealth.Api.Persistence.Models;

namespace App.GitHealth.Api.Persistence.Services;

internal interface IRetentionService
{
    Task<RetentionResult> ApplyAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}
