namespace App.GitHealth.Api.Persistence.Models;

internal sealed record AnalysisFailure(
    string Code,
    string Message,
    DateTimeOffset FailedAtUtc,
    bool IsCancellation = false);
