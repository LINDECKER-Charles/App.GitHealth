namespace App.GitHealth.Api.Features.Analyses;

internal sealed class AnalysisQueueOptions
{
    public const string SectionName = "AnalysisQueue";
    public const int MaximumCapacity = 1024;

    public int Capacity { get; set; } = 32;
}
