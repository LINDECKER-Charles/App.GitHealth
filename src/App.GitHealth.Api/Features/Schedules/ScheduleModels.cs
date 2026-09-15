namespace App.GitHealth.Api.Features.Schedules;

/// <summary>
/// A schedule as it is written. The expression travels even when the switch is off, so that
/// turning it back on does not ask the reader to work it out a second time.
/// </summary>
internal sealed record ScheduleUpdateRequest
{
    public bool IsEnabled { get; init; }

    /// <summary>Five-field cron expression. Null or empty clears whatever was written.</summary>
    public string? CronExpression { get; init; }
}

internal sealed record ScheduleResponse
{
    public required bool IsEnabled { get; init; }

    /// <summary>What was written, normalised to one space between fields.</summary>
    public string? CronExpression { get; init; }

    /// <summary>When the scheduler last launched an analysis of its own accord.</summary>
    public DateTimeOffset? LastRunAtUtc { get; init; }

    /// <summary>
    /// When it will next launch one, computed on reading. Null when the schedule is off, or
    /// when the expression names a date that never comes.
    /// </summary>
    public DateTimeOffset? NextRunAtUtc { get; init; }

    /// <summary>
    /// Zone the expression is read in. Shown rather than assumed: an hour field means nothing
    /// until the reader knows whose clock it is counted on.
    /// </summary>
    public required string TimeZoneId { get; init; }

    /// <summary>
    /// False when the installation has scheduling switched off. The schedule is still stored
    /// and still shown; it simply will not fire, and the interface has to say so.
    /// </summary>
    public required bool IsSchedulerRunning { get; init; }
}
