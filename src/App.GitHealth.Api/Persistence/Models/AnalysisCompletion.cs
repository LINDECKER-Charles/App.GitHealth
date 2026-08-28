using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Api.Persistence.Models;

internal sealed record AnalysisCompletion(
    RepositoryScan Scan,
    DateTimeOffset CompletedAtUtc);
