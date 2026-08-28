using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Api.Git.Models;

internal sealed record RepositoryScanExecution(
    RepositoryScanRequest Request,
    IProgress<RepositoryScanStage>? Progress);
