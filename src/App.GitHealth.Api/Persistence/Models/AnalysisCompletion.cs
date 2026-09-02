using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Api.Persistence.Models;

/// <summary>
/// What a run is aimed at: one project compared against one of its declared baselines.
/// A project with three baselines produces three runs, each with its own target.
/// </summary>
internal readonly record struct AnalysisTarget(Guid ProjectId, string ReferenceName);

internal sealed record AnalysisCompletion(
    RepositoryScan Scan,
    DateTimeOffset CompletedAtUtc);
