namespace App.GitHealth.Core.Branches;

public sealed record ActivityThresholds
{
    public const int DefaultActiveUntilDays = 30;
    public const int DefaultInactiveAfterDays = 90;

    /// <summary>
    /// Échelle réduite des branches sans commit propre. Leur travail est déjà dans la
    /// référence : le compte à rebours peut courir plus vite que pour une branche
    /// dont des commits n'existent nulle part ailleurs.
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
                "Le seuil d’inactivité doit être supérieur au seuil d’activité.");
        }

        return new ActivityThresholds(activeUntilDays, inactiveAfterDays);
    }

    /// <summary>
    /// La plus courte des deux échelles, borne par borne. Un projet qui resserre
    /// lui-même ses seuils n'est donc jamais rallongé par une échelle intégrée.
    /// </summary>
    public ActivityThresholds ShortenTo(ActivityThresholds other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Create(
            Math.Min(ActiveUntilDays, other.ActiveUntilDays),
            Math.Min(InactiveAfterDays, other.InactiveAfterDays));
    }
}
