namespace App.GitHealth.Core.Branches;

public sealed record ActivityThresholds
{
    public const int DefaultActiveUntilDays = 30;
    public const int DefaultInactiveAfterDays = 90;

    public static ActivityThresholds Default { get; } =
        Create(DefaultActiveUntilDays, DefaultInactiveAfterDays);

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
                "Le seuil d’inactivité doit être supérieur au seuil d’activité.");
        }

        return new ActivityThresholds(activeUntilDays, inactiveAfterDays);
    }
}
