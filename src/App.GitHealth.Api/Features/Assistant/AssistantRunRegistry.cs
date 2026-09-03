using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.Assistant;

/// <summary>
/// Holds the runs of the session and caps how many can be in flight. The cap is the point:
/// every run spends the user's own agent quota, so GitHealth never fans out on its own.
/// </summary>
internal sealed class AssistantRunRegistry(IOptions<AssistantOptions> options) : IDisposable
{
    private const int RetainedRuns = 40;
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromMinutes(30);

    private readonly Lock _sync = new();
    private readonly Dictionary<Guid, AssistantRun> _runs = [];

    /// <summary>
    /// Registers a run unless the machine is already busy with as many as it allows. The
    /// count and the insertion share a lock: two clicks arriving together must not both win.
    /// </summary>
    public bool TryRegister(AssistantRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        using (_sync.EnterScope())
        {
            Prune();
            if (_runs.Values.Count(candidate => !candidate.IsFinished)
                >= options.Value.MaximumParallelRuns)
            {
                return false;
            }

            _runs.Add(run.Id, run);
            return true;
        }
    }

    public AssistantRun? Find(Guid runId)
    {
        using (_sync.EnterScope())
        {
            return _runs.GetValueOrDefault(runId);
        }
    }

    public void Dispose()
    {
        using (_sync.EnterScope())
        {
            foreach (var run in _runs.Values)
            {
                run.Dispose();
            }

            _runs.Clear();
        }
    }

    /// <summary>
    /// Drops what nobody will read again: finished runs past the retention window, then the
    /// oldest of whatever is left once the session has accumulated too many.
    /// </summary>
    private void Prune()
    {
        var deadline = DateTimeOffset.UtcNow - RetentionWindow;
        var expired = _runs.Values
            .Where(run => run.IsFinished && run.Descriptor.StartedAtUtc < deadline)
            .ToList();
        var surplus = _runs.Values
            .Where(run => run.IsFinished)
            .Except(expired)
            .OrderBy(run => run.Descriptor.StartedAtUtc)
            .Take(Math.Max(0, _runs.Count - expired.Count - RetainedRuns));
        foreach (var run in expired.Concat(surplus).ToList())
        {
            _runs.Remove(run.Id);
            run.Dispose();
        }
    }
}
