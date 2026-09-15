using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Core.Common;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.Schedules;

/// <summary>
/// What every reader of a schedule needs to agree on: the clock, which zone the expressions
/// are counted on, whether the installation lets them fire at all, and when a given repository
/// is next due. The screen that shows the next firing and the worker that acts on it ask the
/// same object, which is what stops the two of them naming different moments.
/// </summary>
internal sealed class ScheduleContext(IOptions<ScheduleOptions> options, IClock clock)
{
    /// <summary>
    /// Zone the cron expressions are read in. The machine's own unless configured otherwise:
    /// nine in the morning is a local idea, and UTC would be wrong for most readers, twice a
    /// year by an extra hour.
    /// </summary>
    public TimeZoneInfo Zone { get; } = Resolve(options.Value.TimeZone);

    /// <summary>False when the installation has scheduled scanning switched off.</summary>
    public bool IsRunning { get; } = options.Value.Enabled;

    public DateTimeOffset UtcNow => clock.UtcNow;

    /// <summary>
    /// Whether an identifier names a zone this machine knows. The options are validated
    /// against it at startup, so a mistyped zone is reported there rather than silently
    /// swapped for another one months later.
    /// </summary>
    public static bool IsKnownZone(string? identifier) =>
        string.IsNullOrWhiteSpace(identifier)
        || TimeZoneInfo.TryFindSystemTimeZoneById(identifier.Trim(), out _);

    /// <summary>
    /// When this repository is next due, or null when its schedule is off, unreadable, or
    /// names a date that never comes.
    /// </summary>
    public DateTimeOffset? NextRunOf(ProjectEntity project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.ReadSchedule().NextOccurrenceAfter(project.ScheduleAnchor(), Zone);
    }

    private static TimeZoneInfo Resolve(string? identifier) =>
        string.IsNullOrWhiteSpace(identifier)
            ? TimeZoneInfo.Local
            : TimeZoneInfo.FindSystemTimeZoneById(identifier.Trim());
}
