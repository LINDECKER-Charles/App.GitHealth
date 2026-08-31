namespace App.GitHealth.Core.Branches;

public sealed record ActivityThresholds
{
    public const int DefaultActiveUntilDays = 30;
    public const int DefaultInactiveAfterDays = 90;

    /// <summary>
    /// Shortened scale for branches with no own commits. Their work is already in the
    /// baseline: the countdown can run faster than for a branch whose commits exist
    /// nowhere else.
    /// </summary>
    public const int MergedActiveUntilDays = 7;
    public const int MergedInactiveAfterDays = 30;

    public static ActivityThresholds Default { get; } =
        Create(DefaultActiveUntilDays, DefaultInactiveAfterDays);

    public static ActivityThresholds Merged { get; } =
        Create(MergedActiveUntilDays, MergedInactiveAfterDays);

    private ActivityThresholds(int activeUntilDays, int inactiveAfterDays)
    {
        ActiveUntilDays = activeUntilDays;
        InactiveAfterDays = inactiveAfterDays;
    }

    public int ActiveUntilDays { get; }

    public int InactiveAfterDays { get; }

    public static ActivityThresholds Create(int activeUntilDays, int inactiveAfterDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(activeUntilDays);

        if (inactiveAfterDays <= activeUntilDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inactiveAfterDays),
                "The inactivity threshold must be strictly greater than the activity threshold.");
        }

        return new ActivityThresholds(activeUntilDays, inactiveAfterDays);
    }

    /// <summary>
    /// The shorter of the two scales, bound by bound. A project that tightens its own
    /// thresholds is therefore never lengthened by a built-in scale.
    /// </summary>
    public ActivityThresholds ShortenTo(ActivityThresholds other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Create(
            Math.Min(ActiveUntilDays, other.ActiveUntilDays),
            Math.Min(InactiveAfterDays, other.InactiveAfterDays));
    }
}
