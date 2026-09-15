using App.GitHealth.Core.Scheduling;

namespace App.GitHealth.Api.Persistence.Entities;

/// <summary>
/// The scheduling half of a project: when it re-measures itself without being asked, and what
/// the next firing is counted from. Kept apart from the rest because nothing else on the
/// entity reads it, and because the anchor rule below is the whole of the feature's subtlety.
/// </summary>
internal sealed partial class ProjectEntity
{
    /// <summary>
    /// Whether the schedule fires. Held apart from the expression so that switching a
    /// repository off for a fortnight does not throw away what was written for it.
    /// </summary>
    public bool IsScheduleEnabled { get; private set; }

    /// <summary>Five-field cron expression, or null while none has been written.</summary>
    public string? ScheduleCron { get; private set; }

    /// <summary>When the schedule was last written. Null on a project that never had one.</summary>
    public DateTimeOffset? ScheduleChangedAtUtc { get; private set; }

    /// <summary>When the scheduler last launched an analysis of its own accord.</summary>
    public DateTimeOffset? ScheduleLastRunAtUtc { get; private set; }

    /// <summary>
    /// The schedule as the domain reads it. An expression this build cannot parse degrades to
    /// no expression: a row written by another version must never stop a project loading.
    /// </summary>
    public ScanSchedule ReadSchedule() => new()
    {
        IsEnabled = IsScheduleEnabled,
        Expression = CronExpression.ParseOrNull(ScheduleCron),
    };

    /// <summary>
    /// Moment the next firing is counted from: the later of the last firing and the last
    /// edit. Counting from the edit is what stops "save" from launching a run on the spot;
    /// counting from the firing is what makes a window missed while the application was
    /// closed fire once when it opens, rather than once per window gone by.
    /// </summary>
    public DateTimeOffset ScheduleAnchor()
    {
        var edited = ScheduleChangedAtUtc ?? CreatedAtUtc;
        return ScheduleLastRunAtUtc > edited ? ScheduleLastRunAtUtc.Value : edited;
    }

    public void UpdateSchedule(ScanSchedule schedule, DateTimeOffset changedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        UtcDate.Require(changedAtUtc, nameof(changedAtUtc));
        IsScheduleEnabled = schedule.IsEnabled;
        ScheduleCron = schedule.Expression?.Text;
        ScheduleChangedAtUtc = changedAtUtc;
        UpdatedAtUtc = changedAtUtc;
    }

    /// <summary>
    /// Records that the scheduler fired. Written before the analysis is queued: a launch that
    /// fails must not leave the window open for the next tick to fire again.
    /// </summary>
    public void MarkScheduleRun(DateTimeOffset ranAtUtc)
    {
        UtcDate.Require(ranAtUtc, nameof(ranAtUtc));
        ScheduleLastRunAtUtc = ranAtUtc;
    }
}
