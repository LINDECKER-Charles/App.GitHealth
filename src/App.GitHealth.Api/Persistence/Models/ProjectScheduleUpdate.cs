using App.GitHealth.Core.Scheduling;

namespace App.GitHealth.Api.Persistence.Models;

/// <summary>
/// A new schedule for one repository. <see cref="ChangedAtUtc"/> is not bookkeeping: it is the
/// anchor the next firing is counted from, which is what stops a save from firing on the spot.
/// </summary>
internal sealed record ProjectScheduleUpdate(
    Guid ProjectId,
    ScanSchedule Schedule,
    DateTimeOffset ChangedAtUtc);
