namespace App.GitHealth.Api.Features.Analyses;

/// <summary>Turns the live state of a run into what the status endpoint answers.</summary>
internal static class AnalysisProgressMapper
{
    public static AnalysisProgressResponse ToResponse(AnalysisProgressSnapshot progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return new AnalysisProgressResponse
        {
            References = progress.References.Select(ToResponse).ToArray(),
            Commands = progress.Commands.Select(ToResponse).ToArray(),
            CommandCount = progress.CommandCount,
        };
    }

    private static AnalysisReferenceResponse ToResponse(ReferenceProgress reference) => new()
    {
        ReferenceName = reference.ReferenceName,
        CommitId = reference.CommitId,
        State = reference.State.ToString(),
        LastActivityAtUtc = reference.LastActivityAtUtc,
        TipAuthor = reference.TipAuthor,
        MergeBaseCommit = reference.MergeBaseCommit,
        AheadCount = reference.AheadCount,
        BehindCount = reference.BehindCount,
        Topology = reference.Topology?.ToString(),
        TopContributor = reference.TopContributor,
        ContributorCount = reference.ContributorCount,
    };

    private static AnalysisCommandResponse ToResponse(GitCommandEntry command) => new()
    {
        Sequence = command.Sequence,
        CommandLine = command.CommandLine,
        DurationMs = command.DurationMs,
        ExitCode = command.ExitCode,
        Output = command.Output,
    };
}
