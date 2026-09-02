using App.GitHealth.Api.Git.Process;
using App.GitHealth.Api.Git.Scanning;
using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Api.Git.Models;

/// <summary>
/// One run of a scan: what was asked, who is told about it, and the runner its commands go
/// through — traced when somebody follows the run, bare otherwise.
/// </summary>
internal sealed record RepositoryScanExecution(
    RepositoryScanRequest Request,
    ScanReporter Reporter,
    IGitProcessRunner Runner);
