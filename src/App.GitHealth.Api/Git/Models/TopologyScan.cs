namespace App.GitHealth.Api.Git.Models;

internal sealed record TopologyScan(
    CapturedRepository Repository,
    CapturedReference Reference,
    IReadOnlyList<CapturedReference> Branches);
