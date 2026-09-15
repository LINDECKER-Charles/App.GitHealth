namespace App.GitHealth.Core.Scheduling;

/// <summary>
/// The values one cron field allows, resolved once when the expression is parsed. Matching a
/// date is then a table lookup: the scheduler asks the question once a tick, per project, and
/// must never pay for a parse to answer it.
/// </summary>
internal sealed class CronField
{
    private readonly bool[] _allowed;

    private CronField(CronFieldRange range, bool[] allowed, bool isUnrestricted)
    {
        Range = range;
        _allowed = allowed;
        IsUnrestricted = isUnrestricted;
    }

    public CronFieldRange Range { get; }

    /// <summary>
    /// The field was written <c>*</c>, and so names no day in particular. Only the two day
    /// fields read this, to decide whether they combine with "and" or with "or".
    /// </summary>
    public bool IsUnrestricted { get; }

    /// <summary>Every value of the range, as <c>*</c> means it.</summary>
    public static CronField Everything(CronFieldRange range)
    {
        var allowed = new bool[range.Length];
        Array.Fill(allowed, true);
        return new CronField(range, allowed, isUnrestricted: true);
    }

    /// <summary>The named values only; the parser refuses anything outside the range.</summary>
    public static CronField Of(CronFieldRange range, IEnumerable<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var allowed = new bool[range.Length];
        foreach (var value in values)
        {
            allowed[value - range.Minimum] = true;
        }

        return new CronField(range, allowed, isUnrestricted: false);
    }

    public bool Matches(int value) =>
        Range.Contains(value) && _allowed[value - Range.Minimum];
}
