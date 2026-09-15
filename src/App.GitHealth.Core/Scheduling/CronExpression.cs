namespace App.GitHealth.Core.Scheduling;

/// <summary>
/// A five-field cron expression — minute, hour, day of month, month, day of week — read on the
/// wall clock of a given zone rather than in UTC. A reader who writes <c>0 9 * * 1-5</c> means
/// nine in the morning where they are, and would be surprised twice a year by anything else.
/// </summary>
public sealed class CronExpression
{
    /// <summary>
    /// Ceiling on the written form. Far above any real expression, and low enough that a
    /// pasted document is refused before it is parsed field by field.
    /// </summary>
    public const int MaximumLength = 120;

    private const int FieldCount = 5;
    private const int MinuteIndex = 0;
    private const int HourIndex = 1;
    private const int DayOfMonthIndex = 2;
    private const int MonthIndex = 3;
    private const int DayOfWeekIndex = 4;

    /// <summary>
    /// Four years of search before giving up. Long enough for 29 February combined with a
    /// weekday, which is the rarest date a valid expression can name.
    /// </summary>
    private const int MaximumSearchDays = 1461;

    private static readonly CronFieldRange MinuteRange = new(0, 59);
    private static readonly CronFieldRange HourRange = new(0, 23);
    private static readonly CronFieldRange DayOfMonthRange = new(1, 31);
    private static readonly CronFieldRange MonthRange = new(1, 12);

    private readonly CronField[] _fields;

    private CronExpression(string text, CronField[] fields)
    {
        Text = text;
        _fields = fields;
    }

    /// <summary>As stored and shown: trimmed, one space between fields.</summary>
    public string Text { get; }

    private CronField Minute => _fields[MinuteIndex];

    private CronField Hour => _fields[HourIndex];

    private CronField DayOfMonth => _fields[DayOfMonthIndex];

    private CronField Month => _fields[MonthIndex];

    private CronField DayOfWeek => _fields[DayOfWeekIndex];

    /// <exception cref="ArgumentException">The expression is not five readable fields.</exception>
    public static CronExpression Parse(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        if (expression.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"A cron expression cannot exceed {MaximumLength} characters.",
                nameof(expression));
        }

        var parts = expression.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == FieldCount
            ? new CronExpression(string.Join(' ', parts), ParseFields(parts))
            : throw new ArgumentException(
                "A cron expression has five fields: minute, hour, day of month, month "
                + "and day of week.",
                nameof(expression));
    }

    /// <summary>
    /// Non-throwing read, for the values coming back out of the database: a row written by
    /// another build must degrade to "no schedule", never to a failure to load the project.
    /// </summary>
    public static CronExpression? ParseOrNull(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        try
        {
            return Parse(expression);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// First firing strictly after <paramref name="afterUtc"/>, or null when the expression
    /// names a date that never comes — <c>0 0 30 2 *</c> being the honest example.
    /// </summary>
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset afterUtc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var local = TimeZoneInfo.ConvertTime(afterUtc, zone).DateTime;
        var candidate = TruncateToMinute(local).AddMinutes(1);
        var horizon = candidate.Date.AddDays(MaximumSearchDays);
        while (candidate < horizon)
        {
            var match = NextMatchInDay(candidate);
            if (match is null)
            {
                candidate = candidate.Date.AddDays(1);
                continue;
            }

            var instant = Resolve(match.Value, zone);
            if (instant > afterUtc)
            {
                return instant;
            }

            // The clock went back: this wall time already happened. Walk past it rather
            // than hand back an instant the caller has already acted on.
            candidate = match.Value.AddMinutes(1);
        }

        return null;
    }

    public override string ToString() => Text;

    private static CronField[] ParseFields(string[] parts) =>
    [
        CronFieldParser.Parse(parts[MinuteIndex], MinuteRange),
        CronFieldParser.Parse(parts[HourIndex], HourRange),
        CronFieldParser.Parse(parts[DayOfMonthIndex], DayOfMonthRange),
        CronFieldParser.Parse(parts[MonthIndex], MonthRange),
        CronFieldParser.ParseDayOfWeek(parts[DayOfWeekIndex]),
    ];

    /// <summary>
    /// A wall time the zone skipped — the hour lost when the clock goes forward — is read with
    /// the offset in force before the jump, which lands it just after the clock resumed. The
    /// run of that day happens late rather than not at all.
    /// </summary>
    private static DateTimeOffset Resolve(DateTime wallTime, TimeZoneInfo zone) =>
        zone.IsInvalidTime(wallTime)
            ? new DateTimeOffset(wallTime, zone.BaseUtcOffset).ToUniversalTime()
            : new DateTimeOffset(wallTime, zone.GetUtcOffset(wallTime)).ToUniversalTime();

    private static DateTime TruncateToMinute(DateTime value) =>
        value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMinute));

    /// <summary>
    /// First firing at or after <paramref name="candidate"/> on that same date, or null when
    /// the date itself does not match or holds no later slot.
    /// </summary>
    private DateTime? NextMatchInDay(DateTime candidate)
    {
        if (!MatchesDate(candidate))
        {
            return null;
        }

        for (var hour = candidate.Hour; hour <= HourRange.Maximum; hour++)
        {
            var minute = Hour.Matches(hour)
                ? NextMinuteFrom(hour == candidate.Hour ? candidate.Minute : MinuteRange.Minimum)
                : null;
            if (minute is not null)
            {
                return candidate.Date.AddHours(hour).AddMinutes(minute.Value);
            }
        }

        return null;
    }

    private int? NextMinuteFrom(int first)
    {
        for (var minute = first; minute <= MinuteRange.Maximum; minute++)
        {
            if (Minute.Matches(minute))
            {
                return minute;
            }
        }

        return null;
    }

    /// <summary>
    /// The day rule every cron shares: when both day fields name days, a date matching either
    /// one fires. <c>0 0 13 * 5</c> is therefore every 13th and every Friday, not their overlap.
    /// </summary>
    private bool MatchesDate(DateTime date)
    {
        if (!Month.Matches(date.Month))
        {
            return false;
        }

        var byDayOfMonth = DayOfMonth.Matches(date.Day);
        var byDayOfWeek = DayOfWeek.Matches((int)date.DayOfWeek);
        return DayOfMonth.IsUnrestricted || DayOfWeek.IsUnrestricted
            ? byDayOfMonth && byDayOfWeek
            : byDayOfMonth || byDayOfWeek;
    }
}
