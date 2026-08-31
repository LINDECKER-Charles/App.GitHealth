namespace App.GitHealth.Api.Features.Analyses;

internal sealed class AnalysisQueueOptions
{
    public const string SectionName = "AnalysisQueue";
    public const int MaximumCapacity = 1024;
    public const int MinimumTimeoutSeconds = 1;
    public const int MaximumTimeoutSeconds = 3600;
    public const int MinimumParallelAnalyses = 1;
    public const int MaximumParallelAnalysesLimit = 8;

    public int Capacity { get; set; } = 32;

    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Number of analyses run in parallel. At 1, the queue becomes strictly sequential again.
    /// </summary>
    public int MaximumParallelAnalyses { get; set; } = 4;
}
