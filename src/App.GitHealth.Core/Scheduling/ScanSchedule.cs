namespace App.GitHealth.Core.Scheduling;

/// <summary>
/// When a repository re-measures itself without being asked. The switch and the expression are
/// kept apart on purpose: turning a schedule off for a fortnight must not cost the reader the
/// expression they worked out, and turning it back on must not ask them to write it again.
/// </summary>
public sealed record ScanSchedule
{
    /// <summary>No expression, no firing: what a repository has until someone sets one.</summary>
    public static ScanSchedule Disabled { get; } = new();

    public bool IsEnabled { get; init; }

    /// <summary>Null while nothing was written, or while what was written is unreadable.</summary>
    public CronExpression? Expression { get; init; }

    /// <summary>On and able to name a time: the only state the scheduler acts on.</summary>
    public bool IsActive => IsEnabled && Expression is not null;

    /// <summary>
    /// First firing strictly after <paramref name="anchorUtc"/> — the later of the last firing
    /// and the last edit — or null when the schedule is inert or names a date that never comes.
    /// </summary>
    public DateTimeOffset? NextOccurrenceAfter(DateTimeOffset anchorUtc, TimeZoneInfo zone) =>
        IsActive ? Expression!.GetNextOccurrence(anchorUtc, zone) : null;
}
