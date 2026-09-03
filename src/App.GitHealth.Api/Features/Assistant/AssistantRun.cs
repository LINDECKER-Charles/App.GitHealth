using System.Text;
using App.GitHealth.Api.Features.Assistant.Agents.Events;

namespace App.GitHealth.Api.Features.Assistant;

internal enum AssistantRunStatus
{
    Running,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>
/// State of one run of an agent, while it runs. This is the live half only: the trace, the
/// cancellation and the settling. Once it settles the exchange is written to the history and
/// read back from there, so what is kept of a conversation is never this object.
/// </summary>
internal sealed class AssistantRun : IDisposable
{
    /// <summary>
    /// Steps one run may hold. A run that reaches this has stopped saying anything a reader
    /// could follow, and the list is sent whole on every poll — it is not a log.
    /// </summary>
    private const int MaximumSteps = 200;

    private readonly Lock _sync = new();
    private readonly StringBuilder _trace = new();
    private readonly List<AssistantRunStep> _steps = [];
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

    /// <summary>
    /// Records what the agent has just started doing. The same activity twice running is
    /// kept once — an agent that asks the model three times in a row is doing one thing —
    /// and the list stops growing at <see cref="MaximumSteps" />.
    /// </summary>
    public void AppendStep(AgentStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        var recorded = new AssistantRunStep
        {
            Kind = step.Kind.ToString(),
            Label = step.Label,
            Detail = step.Detail,
            AtUtc = DateTimeOffset.UtcNow,
        };
        using (_sync.EnterScope())
        {
            if (_steps.Count < MaximumSteps && !Repeats(recorded))
            {
                _steps.Add(recorded);
            }
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
                ConversationId = Descriptor.ConversationId,
                BranchCount = Descriptor.BranchCount,
                Status = _status.ToString(),
                StartedAtUtc = Descriptor.StartedAtUtc,
                CompletedAtUtc = _completedAtUtc,
                Steps = [.. _steps],
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

    /// <summary>The same activity as the one before it, the moment it happened aside.</summary>
    private bool Repeats(AssistantRunStep step) =>
        _steps.Count > 0 && _steps[^1] with { AtUtc = step.AtUtc } == step;

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

    /// <summary>Thread this run belongs to, whether it opened it or continued it.</summary>
    public required Guid ConversationId { get; init; }

    /// <summary>Rows of the capture the agent may read, which bounds any count it gives.</summary>
    public required int BranchCount { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }
}

/// <summary>
/// One thing the agent did, as the panel narrates it. Sent while the run is in flight and
/// never afterwards: the steps are what replaces a spinner, not a record of anything.
/// </summary>
internal sealed record AssistantRunStep
{
    /// <summary>Waiting, Thinking, Tool or Writing, phrased by the interface.</summary>
    public required string Kind { get; init; }

    /// <summary>The capture tool that was called. Empty for every other kind.</summary>
    public required string Label { get; init; }

    /// <summary>What the call asked for, or what the agent said of its own reasoning.</summary>
    public string? Detail { get; init; }

    public required DateTimeOffset AtUtc { get; init; }
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

    public required Guid ConversationId { get; init; }

    public required int BranchCount { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    /// <summary>
    /// What the agent has been doing, oldest first, whole rather than since an offset: the
    /// list is bounded and small, and sending it whole makes a poll worth nothing more than
    /// the one before it — a dropped answer costs a frame, never a step.
    /// </summary>
    public required IReadOnlyList<AssistantRunStep> Steps { get; init; }

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
