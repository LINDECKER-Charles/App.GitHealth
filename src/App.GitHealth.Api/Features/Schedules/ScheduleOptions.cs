namespace App.GitHealth.Api.Features.Schedules;

/// <summary>
/// Settings of the scheduler. It is the one part of GitHealth that starts work nobody asked
/// for at that moment, so it is also the one with a switch that turns it off outright.
/// </summary>
public sealed class ScheduleOptions
{
    public const string SectionName = "GitHealth:Schedule";
    public const int MinimumTickSeconds = 1;
    public const int MaximumTickSeconds = 3600;

    /// <summary>
    /// Turns scheduled scanning off for the whole installation. Saved schedules are kept and
    /// shown, they simply never fire — a machine that must not spend its evenings reading
    /// repositories is configured once, and no interface can undo it.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// How often the due schedules are looked for. Cron resolves to the minute, so anything
    /// under a minute only decides how late a firing can be, never whether it happens.
    /// </summary>
    public int TickSeconds { get; init; } = 30;

    /// <summary>
    /// Zone the expressions are read in, as an identifier the system knows — <c>Europe/Paris</c>,
    /// <c>UTC</c>. Empty means the machine's own zone, which is what "9 in the morning" means
    /// to whoever wrote it.
    /// </summary>
    public string? TimeZone { get; init; }
}
