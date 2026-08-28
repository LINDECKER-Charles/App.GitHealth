using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Api.Features.Analyses;

internal sealed class InlineScanProgress(Action<RepositoryScanStage> report)
    : IProgress<RepositoryScanStage>
{
    public void Report(RepositoryScanStage value) => report(value);
}
