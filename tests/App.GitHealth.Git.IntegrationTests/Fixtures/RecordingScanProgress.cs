using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Git.IntegrationTests.Fixtures;

/// <summary>
/// Records the events of a scan as they are reported. Deliberately not
/// <see cref="Progress{T}"/>: that one hands the callback to the thread pool, which would
/// scramble the order and let events land after the scan has already returned.
/// </summary>
internal sealed class RecordingScanProgress : IProgress<RepositoryScanEvent>
{
    private readonly List<RepositoryScanEvent> _events = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<RepositoryScanEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return _events.ToArray();
            }
        }
    }

    public IEnumerable<TEvent> Of<TEvent>()
        where TEvent : RepositoryScanEvent => Events.OfType<TEvent>();

    public void Report(RepositoryScanEvent value)
    {
        lock (_gate)
        {
            _events.Add(value);
        }
    }
}
