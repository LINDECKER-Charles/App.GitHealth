using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Features.Analyses;

/// <summary>
/// Live state of one running analysis. Written by the worker reading the repository, read by
/// whoever polls the status: every method takes the lock, and readers only ever get a copy.
/// </summary>
internal sealed class AnalysisRunProgress
{
    /// <summary>
    /// A big repository runs thousands of commands. Keeping the last few is enough for a
    /// console that scrolls, and keeps the status answer small.
    /// </summary>
    private const int RetainedCommands = 60;

    private readonly Queue<GitCommandEntry> _commands = new();
    private readonly Lock _gate = new();
    private readonly Dictionary<string, int> _rankByReference = new(StringComparer.Ordinal);
    private readonly List<ReferenceProgress> _references = [];
    private int _commandCount;
    private string? _message;
    private AnalysisPhase _phase = AnalysisPhase.Waiting;

    public void SetPhase(AnalysisPhase phase, string? message = null)
    {
        lock (_gate)
        {
            _phase = phase;
            _message = message;
        }
    }

    public void List(IReadOnlyList<ScannedReferenceListing> references)
    {
        lock (_gate)
        {
            _references.Clear();
            _rankByReference.Clear();
            foreach (var reference in references)
            {
                _rankByReference[reference.ReferenceName] = _references.Count;
                _references.Add(ToProgress(reference));
            }
        }
    }

    public void Start(string referenceName, RepositoryScanStage stage)
    {
        var state = stage == RepositoryScanStage.Topology
            ? ReferenceProgressState.Measuring
            : ReferenceProgressState.Enriching;
        Amend(referenceName, reference => reference with { State = state });
    }

    public void Measure(ScanReferenceMeasured measured)
    {
        ArgumentNullException.ThrowIfNull(measured);
        var divergence = measured.Divergence;
        Amend(measured.ReferenceName, reference => reference with
        {
            State = ReferenceProgressState.Measured,
            MergeBaseCommit = measured.MergeBaseCommit,
            AheadCount = divergence.AheadCount,
            BehindCount = divergence.BehindCount,
            Topology = BranchClassifier.ClassifyTopology(divergence),
        });
    }

    public void Enrich(ScanReferenceEnriched enriched)
    {
        ArgumentNullException.ThrowIfNull(enriched);
        Amend(enriched.ReferenceName, reference => reference with
        {
            State = ReferenceProgressState.Read,
            TopContributor = enriched.TopContributor,
            ContributorCount = enriched.ContributorCount,
        });
    }

    public void Record(ScanCommandCompleted command)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (_gate)
        {
            _commandCount += 1;
            _commands.Enqueue(new GitCommandEntry
            {
                Sequence = _commandCount,
                CommandLine = command.CommandLine,
                DurationMs = (int)Math.Round(command.Duration.TotalMilliseconds),
                ExitCode = command.ExitCode,
                Output = command.Output,
            });
            while (_commands.Count > RetainedCommands)
            {
                _commands.Dequeue();
            }
        }
    }

    public AnalysisProgressSnapshot Snapshot(DateTimeOffset updatedAtUtc)
    {
        lock (_gate)
        {
            return new AnalysisProgressSnapshot
            {
                Phase = _phase,
                Message = _message,
                UpdatedAtUtc = updatedAtUtc,
                References = _references.ToArray(),
                Commands = _commands.ToArray(),
                CommandCount = _commandCount,
            };
        }
    }

    private void Amend(string referenceName, Func<ReferenceProgress, ReferenceProgress> amend)
    {
        lock (_gate)
        {
            if (_rankByReference.TryGetValue(referenceName, out var rank))
            {
                _references[rank] = amend(_references[rank]);
            }
        }
    }

    private static ReferenceProgress ToProgress(ScannedReferenceListing reference) => new()
    {
        ReferenceName = reference.ReferenceName,
        CommitId = reference.CommitId,
        State = ReferenceProgressState.Listed,
        LastActivityAtUtc = reference.LastActivityAtUtc,
        TipAuthor = reference.TipAuthor,
    };
}
