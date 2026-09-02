namespace App.GitHealth.Api.Features.Assistant.Agents;

/// <summary>
/// Outcome of the search for one agent: the path selected, and enough to make its absence
/// actionable rather than a greyed-out button with no explanation.
/// </summary>
internal sealed record AgentLocation
{
    public required AgentDefinition Agent { get; init; }

    /// <summary>Path selected, or <see langword="null" /> when no candidate exists.</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>
    /// Version the executable reported, filled in once it has actually answered. An
    /// executable that exists but cannot run is not an available agent.
    /// </summary>
    public string? Version { get; init; }

    public required IReadOnlyList<string> SearchedDirectories { get; init; }

    public bool IsResolved => ExecutablePath is not null;

    public string UnavailableMessage =>
        $"{Agent.DisplayName} was not found. Directories tried: the PATH"
        + $"{DescribeSearchedDirectories()}. Install it from {Agent.InstallationUrl}, or point "
        + $"at the executable with the {Agent.ConfigurationKey} setting.";

    private string DescribeSearchedDirectories() => SearchedDirectories.Count == 0
        ? string.Empty
        : $", {string.Join(", ", SearchedDirectories)}";
}
