namespace App.GitHealth.Api.Features.Updates;

/// <summary>Update state, as served by <c>GET /api/updates</c>.</summary>
internal sealed record UpdateStatus
{
    /// <summary>
    /// Name of an <see cref="UpdateAvailability" />. As with the other API responses,
    /// the contract stays textual rather than numeric.
    /// </summary>
    public required string Availability { get; init; }

    /// <summary>
    /// Installed version, or <see langword="null" /> outside a managed installation.
    /// </summary>
    public string? CurrentVersion { get; init; }

    /// <summary>Newer released version, set only when one exists.</summary>
    public string? AvailableVersion { get; init; }

    public static UpdateStatus Unsupported { get; } = For(UpdateAvailability.Unsupported);

    public static UpdateStatus For(
        UpdateAvailability availability,
        string? currentVersion = null,
        string? availableVersion = null)
    {
        return new UpdateStatus
        {
            Availability = availability.ToString(),
            CurrentVersion = currentVersion,
            AvailableVersion = availableVersion,
        };
    }
}
