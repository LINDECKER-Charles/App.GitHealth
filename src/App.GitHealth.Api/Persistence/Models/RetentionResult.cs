namespace App.GitHealth.Api.Persistence.Models;

internal sealed record RetentionResult(bool IsEnabled, int DeletedAnalysisCount);
