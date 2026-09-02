namespace App.GitHealth.Core.Assistant;

/// <summary>
/// Thresholds and patterns in force when the capture was read. Without them a verdict is
/// unreadable: "inactive" only means something against the delay that produced it.
/// </summary>
public sealed record BriefingPolicy
{
    public required int ActiveUntilDays { get; init; }

    public required int InactiveAfterDays { get; init; }

    public required IReadOnlyList<string> ProtectedPatterns { get; init; }

    public required IReadOnlyList<string> ExcludedPatterns { get; init; }
}
