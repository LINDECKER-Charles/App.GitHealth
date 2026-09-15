using App.GitHealth.Core.Scheduling;

namespace App.GitHealth.Core.Tests.Scheduling;

public sealed class CronExpressionTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    // Wednesday 2026-09-16, 08:17 UTC: a minute that matches nothing on its own, so every
    // expectation below is the expression's doing and not the starting point's.
    private static readonly DateTimeOffset Wednesday =
        new(2026, 9, 16, 8, 17, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("* * * * *", "2026-09-16T08:18:00Z")]
    [InlineData("*/15 * * * *", "2026-09-16T08:30:00Z")]
    [InlineData("0 * * * *", "2026-09-16T09:00:00Z")]
    [InlineData("0 9 * * *", "2026-09-16T09:00:00Z")]
    [InlineData("0 8 * * *", "2026-09-17T08:00:00Z")]
    [InlineData("30 8 * * *", "2026-09-16T08:30:00Z")]
    [InlineData("0 0 1 * *", "2026-10-01T00:00:00Z")]
    [InlineData("17 8 * * *", "2026-09-17T08:17:00Z")]
    public void NextOccurrenceFollowsTheExpression(string expression, string expected)
    {
        var next = CronExpression.Parse(expression).GetNextOccurrence(Wednesday, Utc);

        Assert.Equal(DateTimeOffset.Parse(expected, null), next);
    }

    // The anchor is a Wednesday morning. Saturday and Sunday are 6 and 0, and a run already
    // past for today has to wait a whole week: both are where an off-by-one shows.
    [Theory]
    [InlineData("0 9 * * 1-5", "2026-09-16T09:00:00Z")]
    [InlineData("0 8 * * 1-5", "2026-09-17T08:00:00Z")]
    [InlineData("0 9 * * 6", "2026-09-19T09:00:00Z")]
    [InlineData("0 9 * * 0", "2026-09-20T09:00:00Z")]
    [InlineData("0 9 * * 7", "2026-09-20T09:00:00Z")]
    [InlineData("0 8 * * 3", "2026-09-23T08:00:00Z")]
    public void DayOfWeekAcceptsBothSpellingsOfSunday(string expression, string expected)
    {
        var next = CronExpression.Parse(expression).GetNextOccurrence(Wednesday, Utc);

        Assert.Equal(DateTimeOffset.Parse(expected, null), next);
    }

    /// <summary>
    /// The rule every cron shares: two restricted day fields are read as "or". The 18th is a
    /// Friday, so the Monday constraint must not suppress it.
    /// </summary>
    [Fact]
    public void RestrictedDayFieldsFireOnEitherOfThem()
    {
        var expression = CronExpression.Parse("0 9 18 * 1");

        var next = expression.GetNextOccurrence(Wednesday, Utc);

        Assert.Equal(new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.Zero), next);
    }

    /// <summary>With one day field left as <c>*</c>, the other one alone decides.</summary>
    [Fact]
    public void UnrestrictedDayFieldDoesNotWidenTheOther()
    {
        var expression = CronExpression.Parse("0 9 18 * *");

        var next = expression.GetNextOccurrence(Wednesday, Utc);

        Assert.Equal(new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void NextOccurrenceIsStrictlyAfterTheAnchor()
    {
        var onTheMinute = new DateTimeOffset(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);

        var next = CronExpression.Parse("0 9 * * *").GetNextOccurrence(onTheMinute, Utc);

        Assert.Equal(new DateTimeOffset(2026, 9, 17, 9, 0, 0, TimeSpan.Zero), next);
    }

    /// <summary>A date that never comes answers null, not a search that never ends.</summary>
    [Fact]
    public void ImpossibleDateHasNoOccurrence()
    {
        var expression = CronExpression.Parse("0 0 30 2 *");

        Assert.Null(expression.GetNextOccurrence(Wednesday, Utc));
    }

    [Fact]
    public void LeapDayIsFoundWithinTheSearchHorizon()
    {
        var expression = CronExpression.Parse("0 0 29 2 *");

        var next = expression.GetNextOccurrence(Wednesday, Utc);

        Assert.Equal(new DateTimeOffset(2028, 2, 29, 0, 0, 0, TimeSpan.Zero), next);
    }

    [Theory]
    [InlineData("0 9 * * *", "0 9 * * *")]
    [InlineData("  0   9  *  *  * ", "0 9 * * *")]
    [InlineData("0,30 9-17 * * 1-5", "0,30 9-17 * * 1-5")]
    public void TextIsNormalizedToSingleSpaces(string written, string expected)
    {
        Assert.Equal(expected, CronExpression.Parse(written).Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("* * * *")]
    [InlineData("* * * * * *")]
    [InlineData("60 * * * *")]
    [InlineData("* 24 * * *")]
    [InlineData("* * 0 * *")]
    [InlineData("* * 32 * *")]
    [InlineData("* * * 13 *")]
    [InlineData("* * * * 8")]
    [InlineData("-1 * * * *")]
    [InlineData("5-1 * * * *")]
    [InlineData("*/0 * * * *")]
    [InlineData("*/61 * * * *")]
    [InlineData("*/1/2 * * * *")]
    [InlineData("a * * * *")]
    [InlineData("0 9 * * MON")]
    public void ParseRejectsUnreadableExpressions(string expression)
    {
        Assert.Throws<ArgumentException>(() => CronExpression.Parse(expression));
    }

    [Theory]
    [InlineData("0 9 * * *", true)]
    [InlineData("nonsense", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ParseOrNullAnswersRatherThanThrows(string? expression, bool expected)
    {
        Assert.Equal(expected, CronExpression.ParseOrNull(expression) is not null);
    }

    /// <summary>
    /// Nine in the morning means nine on the reader's clock. Paris is two hours ahead of UTC
    /// in September, so the same expression names 07:00 UTC there and 09:00 UTC in Greenwich.
    /// </summary>
    [Fact]
    public void ExpressionIsReadOnTheWallClockOfItsZone()
    {
        var paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

        var next = CronExpression.Parse("0 9 * * *").GetNextOccurrence(Wednesday, paris);

        Assert.Equal(new DateTimeOffset(2026, 9, 17, 7, 0, 0, TimeSpan.Zero), next);
    }

    /// <summary>
    /// The hour the clock skips forward over does not exist on the wall. The run of that day
    /// still happens, pushed to the moment the clock resumed, rather than being dropped.
    /// </summary>
    [Fact]
    public void SkippedWallTimeStillFires()
    {
        var paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
        var beforeTheJump = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);

        var next = CronExpression.Parse("30 2 * * *").GetNextOccurrence(beforeTheJump, paris);

        Assert.NotNull(next);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero), next);
    }
}
