using System.Text;

namespace App.GitHealth.Api.Features.Assistant;

internal enum AssistantRunStatus
{
    Running,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>
/// State of one conversation with an agent. Runs are held in memory only: an answer is
/// worth reading while the window is open, and persisting it would put the repository's
/// branch names — and whatever the user asked — into the exportable database.
/// </summary>
internal sealed class AssistantRun : IDisposable
{
    private readonly Lock _sync = new();
    private readonly StringBuilder _trace = new();
    private readonly CancellationTokenSource _cancellation = new();
    private AssistantRunStatus _status = AssistantRunStatus.Running;
    private DateTimeOffset? _completedAtUtc;
    private string? _answer;
    private string? _failureCode;
    private string? _failureMessage;
    private bool _isTruncated;

    public AssistantRun(AssistantRunDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
    }

    public AssistantRunDescriptor Descriptor { get; }

    public Guid Id => Descriptor.RunId;

    public CancellationToken CancellationToken => _cancellation.Token;

    public bool IsFinished
    {
        get
        {
            using (_sync.EnterScope())
            {
                return _status != AssistantRunStatus.Running;
            }
        }
    }

    public void AppendTrace(string chunk)
    {
        using (_sync.EnterScope())
        {
            _trace.Append(chunk);
        }
    }

    public void Complete(string answer, bool isTruncated)
    {
        using (_sync.EnterScope())
        {
            Settle(AssistantRunStatus.Completed);
            _answer = answer;
            _isTruncated = isTruncated;
        }
    }

    public void Fail(string code, string message)
    {
        using (_sync.EnterScope())
        {
            Settle(AssistantRunStatus.Failed);
            _failureCode = code;
            _failureMessage = message;
        }
    }

    public void MarkCancelled()
    {
        using (_sync.EnterScope())
        {
            Settle(AssistantRunStatus.Cancelled);
        }
    }

    /// <summary>Asks the process to stop; the run settles when it actually has.</summary>
    public void RequestCancellation()
    {
        if (!_cancellation.IsCancellationRequested)
        {
            _cancellation.Cancel();
        }
    }

    /// <summary>
    /// Reads the run from <paramref name="from" /> characters into the trace, so a poll
    /// carries what appeared since the last one rather than the whole log every second.
    /// </summary>
    public AssistantRunSnapshot Read(int from)
    {
        using (_sync.EnterScope())
        {
            var offset = Math.Clamp(from, 0, _trace.Length);
            return new AssistantRunSnapshot
            {
                RunId = Descriptor.RunId,
                ProjectId = Descriptor.ProjectId,
                AgentId = Descriptor.AgentId,
                AgentName = Descriptor.AgentName,
                Effort = Descriptor.Effort,
                Question = Descriptor.Question,
                CommandLine = Descriptor.CommandLine,
                Status = _status.ToString(),
                StartedAtUtc = Descriptor.StartedAtUtc,
                CompletedAtUtc = _completedAtUtc,
                Trace = _trace.ToString(offset, _trace.Length - offset),
                TraceOffset = _trace.Length,
                Answer = _answer,
                FailureCode = _failureCode,
                FailureMessage = _failureMessage,
                IsTruncated = _isTruncated,
            };
        }
    }

    public void Dispose() => _cancellation.Dispose();

    /// <summary>First outcome wins: a cancellation racing a completion must not rewrite it.</summary>
    private void Settle(AssistantRunStatus status)
    {
        if (_status != AssistantRunStatus.Running)
        {
            return;
        }

        _status = status;
        _completedAtUtc = DateTimeOffset.UtcNow;
    }
}

/// <summary>What a run is, fixed the moment it starts.</summary>
internal sealed record AssistantRunDescriptor
{
    public required Guid RunId { get; init; }

    public required Guid ProjectId { get; init; }

    public required string AgentId { get; init; }

    public required string AgentName { get; init; }

    /// <summary>The level actually used, which may be the agent's default.</summary>
    public required string Effort { get; init; }

    public required string Question { get; init; }

    /// <summary>Shown as it will be run, so the command is never a black box.</summary>
    public required string CommandLine { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }
}

internal sealed record AssistantRunSnapshot
{
    public required Guid RunId { get; init; }

    public required Guid ProjectId { get; init; }

    public required string AgentId { get; init; }

    public required string AgentName { get; init; }

    public required string Effort { get; init; }

    public required string Question { get; init; }

    public required string CommandLine { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    /// <summary>What the agent has written since the offset the caller asked from.</summary>
    public required string Trace { get; init; }

    /// <summary>Offset to send back on the next poll.</summary>
    public required int TraceOffset { get; init; }

    public string? Answer { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }

    /// <summary>The agent wrote more than the budget allowed and was stopped short.</summary>
    public bool IsTruncated { get; init; }
}
