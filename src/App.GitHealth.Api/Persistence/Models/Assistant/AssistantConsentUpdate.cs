namespace App.GitHealth.Api.Persistence.Models.Assistant;

/// <summary>
/// A decision about whether this repository's captures may be sent to an agent. Granting
/// carries the moment it was granted, because that is what the interface shows back; a
/// withdrawal carries none, and reads as "never allowed" again.
/// </summary>
internal sealed record AssistantConsentUpdate
{
    public required Guid ProjectId { get; init; }

    /// <summary>Null withdraws the permission.</summary>
    public required DateTimeOffset? GrantedAtUtc { get; init; }

    public required DateTimeOffset ChangedAtUtc { get; init; }
}
