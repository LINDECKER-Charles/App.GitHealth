namespace App.GitHealth.Core.Scheduling;

/// <summary>
/// Bounds one cron field accepts, inclusive on both ends. Carrying the pair together is what
/// lets a single parser serve five fields that agree on nothing but their shape.
/// </summary>
internal readonly record struct CronFieldRange(int Minimum, int Maximum)
{
    /// <summary>Number of distinct values the field can take, bounds included.</summary>
    public int Length => Maximum - Minimum + 1;

    public bool Contains(int value) => value >= Minimum && value <= Maximum;
}
