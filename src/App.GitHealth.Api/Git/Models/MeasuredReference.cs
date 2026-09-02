using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Git.Models;

/// <summary>
/// One reference placed against the baseline, with the commit their histories share when
/// the comparison established one.
/// </summary>
internal sealed record MeasuredReference(BranchDivergence Divergence, string? MergeBaseCommit);
