using App.GitHealth.Core.Scheduling;

namespace App.GitHealth.Core.Tests.Scheduling;

public sealed class ScanScheduleTests
{
    private static readonly DateTimeOffset Anchor =
        new(2026, 9, 16, 8, 17, 0, TimeSpan.Zero);

    [Fact]
    public void DisabledScheduleNamesNoOccurrence()
    {
        Assert.False(ScanSchedule.Disabled.IsActive);
        Assert.Null(ScanSchedule.Disabled.NextOccurrenceAfter(Anchor, TimeZoneInfo.Utc));
    }

    /// <summary>
    /// Switched off, the expression is still held. That is the whole point of keeping the two
    /// apart: a fortnight of silence must not cost the reader what they wrote.
    /// </summary>
    [Fact]
    public void ExpressionSurvivesBeingSwitchedOff()
    {
        var schedule = new ScanSchedule
        {
            IsEnabled = false,
            Expression = CronExpression.Parse("0 9 * * *"),
        };

        Assert.False(schedule.IsActive);
        Assert.Null(schedule.NextOccurrenceAfter(Anchor, TimeZoneInfo.Utc));
        Assert.Equal("0 9 * * *", schedule.Expression!.Text);
    }

    [Fact]
    public void EnabledScheduleWithoutExpressionStaysInert()
    {
        var schedule = new ScanSchedule { IsEnabled = true };

        Assert.False(schedule.IsActive);
        Assert.Null(schedule.NextOccurrenceAfter(Anchor, TimeZoneInfo.Utc));
    }

    [Fact]
    public void ActiveScheduleNamesItsNextOccurrence()
    {
        var schedule = new ScanSchedule
        {
            IsEnabled = true,
            Expression = CronExpression.Parse("0 9 * * *"),
        };

        var next = schedule.NextOccurrenceAfter(Anchor, TimeZoneInfo.Utc);

        Assert.True(schedule.IsActive);
        Assert.Equal(new DateTimeOffset(2026, 9, 16, 9, 0, 0, TimeSpan.Zero), next);
    }
}
