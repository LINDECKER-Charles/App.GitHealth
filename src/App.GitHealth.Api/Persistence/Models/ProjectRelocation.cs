namespace App.GitHealth.Api.Persistence.Models;

internal sealed record ProjectRelocation(
    Guid ProjectId,
    string RepositoryPath,
    DateTimeOffset ChangedAtUtc);
