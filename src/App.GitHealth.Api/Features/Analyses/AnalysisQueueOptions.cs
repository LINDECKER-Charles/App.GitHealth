namespace App.GitHealth.Api.Features.Analyses;

internal sealed class AnalysisQueueOptions
{
    public const string SectionName = "AnalysisQueue";
    public const int MaximumCapacity = 1024;
    public const int MinimumTimeoutSeconds = 1;
    public const int MaximumTimeoutSeconds = 3600;

    public int Capacity { get; set; } = 32;

    public int TimeoutSeconds { get; set; } = 300;
}
