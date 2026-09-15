using System.Globalization;

namespace App.GitHealth.Core.Scheduling;

/// <summary>
/// Reads one field of a cron expression — <c>*</c>, <c>5</c>, <c>1-5</c>, <c>*/15</c>,
/// <c>1-5/2</c>, <c>9/2</c>, and any of those in a comma-separated list. Names such as
/// <c>MON</c> are deliberately not accepted: the interface writes numbers, and a form that
/// takes two spellings has to explain both.
/// </summary>
internal static class CronFieldParser
{
    private const char ListSeparator = ',';
    private const char RangeSeparator = '-';
    private const char StepSeparator = '/';
    private const string Wildcard = "*";

    /// <summary>Sunday is written 0 or 7, so the day of week is read over eight values.</summary>
    private static readonly CronFieldRange WrittenDayOfWeek = new(0, 7);

    private static readonly CronFieldRange DayOfWeek = new(0, 6);

    public static CronField Parse(string text, CronFieldRange range)
    {
        var values = Collect(text, range);
        return values is null ? CronField.Everything(range) : CronField.Of(range, values);
    }

    /// <summary>
    /// The day of week, with 7 folded onto 0. Both spellings of Sunday therefore land on the
    /// same day, and <c>0-7</c> names the same seven days as <c>*</c>.
    /// </summary>
    public static CronField ParseDayOfWeek(string text)
    {
        var values = Collect(text, WrittenDayOfWeek);
        return values is null
            ? CronField.Everything(DayOfWeek)
            : CronField.Of(DayOfWeek, values.Select(FoldSunday));
    }

    /// <summary>
    /// Rejection carrying the fragment that caused it: "a bad expression" sends a reader back
    /// to five fields, "'62' is not valid" sends them to the one they mistyped.
    /// </summary>
    public static ArgumentException Invalid(string expression) => new(
        $"'{expression}' is not a valid part of a cron expression.",
        nameof(expression));

    /// <summary>Null stands for <c>*</c>: no value is named, which the day rule reads.</summary>
    private static SortedSet<int>? Collect(string text, CronFieldRange range)
    {
        if (string.Equals(text, Wildcard, StringComparison.Ordinal))
        {
            return null;
        }

        var values = new SortedSet<int>();
        foreach (var item in text.Split(ListSeparator))
        {
            AddItem(item, range, values);
        }

        return values;
    }

    private static void AddItem(string item, CronFieldRange range, SortedSet<int> values)
    {
        var parts = item.Split(StepSeparator);
        if (parts.Length > 2)
        {
            throw Invalid(item);
        }

        var hasStep = parts.Length == 2;
        var step = hasStep ? ParseStep(parts[1], range) : 1;
        var bounds = ParseBounds(parts[0], range, hasStep);
        for (var value = bounds.Minimum; value <= bounds.Maximum; value += step)
        {
            values.Add(value);
        }
    }

    private static CronFieldRange ParseBounds(string spec, CronFieldRange range, bool hasStep)
    {
        if (string.Equals(spec, Wildcard, StringComparison.Ordinal))
        {
            return range;
        }

        var edges = spec.Split(RangeSeparator);
        if (edges.Length == 2)
        {
            return Bounded(ParseValue(edges[0], range), ParseValue(edges[1], range), spec);
        }

        if (edges.Length != 1)
        {
            throw Invalid(spec);
        }

        // `9/2` reads as "every 2 from 9", the way `*/2` reads as "every 2 from the start".
        var single = ParseValue(edges[0], range);
        return new CronFieldRange(single, hasStep ? range.Maximum : single);
    }

    private static CronFieldRange Bounded(int low, int high, string spec) => low <= high
        ? new CronFieldRange(low, high)
        : throw Invalid(spec);

    private static int ParseValue(string text, CronFieldRange range)
    {
        var isNumber = int.TryParse(
            text,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var value);
        return isNumber && range.Contains(value) ? value : throw Invalid(text);
    }

    private static int ParseStep(string text, CronFieldRange range)
    {
        var isNumber = int.TryParse(
            text,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var step);
        return isNumber && step >= 1 && step <= range.Length ? step : throw Invalid(text);
    }

    private static int FoldSunday(int day) => day == WrittenDayOfWeek.Maximum
        ? DayOfWeek.Minimum
        : day;
}
