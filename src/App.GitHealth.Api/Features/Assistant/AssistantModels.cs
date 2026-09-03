using App.GitHealth.Api.Features.Assistant.Agents;
using App.GitHealth.Core.Assistant;
using Microsoft.AspNetCore.Mvc;

namespace App.GitHealth.Api.Features.Assistant;

/// <summary>What the interface asks for when it starts a run.</summary>
internal sealed record AssistantRunRequest
{
    /// <summary>Identifier of a catalog agent. Anything else is refused, never launched.</summary>
    public string? AgentId { get; init; }

    public string? Question { get; init; }

    /// <summary>Baseline whose capture is briefed. Absent means the project's primary one.</summary>
    public string? Baseline { get; init; }

    /// <summary>
    /// How hard the agent is asked to think. Absent means the agent's own default; a level
    /// the agent does not declare is refused rather than passed on to its command line.
    /// </summary>
    public string? Effort { get; init; }

    /// <summary>
    /// Thread this run continues. Absent starts a new one, whose identifier comes back on
    /// the snapshot so the next question can name it.
    /// </summary>
    public Guid? ConversationId { get; init; }
}

/// <summary>
/// The capture a run works from: the measurements themselves, and the identifier of the
/// analysis that took them. The identifier never reaches the agent — it is what a stored
/// conversation hangs off, so that deleting the capture takes the conversation with it.
/// </summary>
internal sealed record AssistantCapture
{
    public required Guid AnalysisId { get; init; }

    public required AnalysisBriefing Briefing { get; init; }

    /// <summary>
    /// When this repository's captures were allowed to be sent, or null while they never
    /// were. A run reads it rather than trusting the panel: the permission is what lets
    /// anything leave the machine, so refusing without it has to happen here.
    /// </summary>
    public required DateTimeOffset? ConsentGrantedAtUtc { get; init; }
}

internal sealed record AssistantBriefingQueryParameters
{
    [FromQuery(Name = "baseline")]
    public string? Baseline { get; init; }
}

internal sealed record AssistantAgentsQueryParameters
{
    /// <summary>Probes again rather than reading the session's answer, for a late install.</summary>
    [FromQuery(Name = "refresh")]
    public bool? Refresh { get; init; }
}

internal sealed record AssistantRunQueryParameters
{
    /// <summary>Characters of the trace already received, so a poll only carries the rest.</summary>
    [FromQuery(Name = "from")]
    public int? From { get; init; }
}

internal sealed record AssistantAgentResponse
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required bool IsAvailable { get; init; }

    /// <summary>What the CLI answered to its version flag, once it has actually answered.</summary>
    public string? Version { get; init; }

    public string? ExecutablePath { get; init; }

    public required string InstallationUrl { get; init; }

    /// <summary>Where the search looked and what to do about it, when nothing was found.</summary>
    public string? UnavailableReason { get; init; }

    /// <summary>Effort levels this agent accepts, cheapest first.</summary>
    public required IReadOnlyList<string> Efforts { get; init; }

    public required string DefaultEffort { get; init; }

    public static AssistantAgentResponse From(AgentLocation location) => new()
    {
        Id = location.Agent.Id,
        Name = location.Agent.DisplayName,
        IsAvailable = location.Version is not null,
        Version = location.Version,
        ExecutablePath = location.ExecutablePath,
        InstallationUrl = location.Agent.InstallationUrl,
        UnavailableReason = location.Version is null ? location.UnavailableMessage : null,
        Efforts = location.Agent.Efforts,
        DefaultEffort = location.Agent.DefaultEffort,
    };
}

internal sealed record AssistantAgentListResponse
{
    /// <summary>False turns the whole feature off, whatever is installed on the machine.</summary>
    public required bool IsEnabled { get; init; }

    public required IReadOnlyList<AssistantAgentResponse> Agents { get; init; }
}

/// <summary>
/// The briefing, shown before anything is sent. This is the whole consent mechanism: the
/// user reads the exact text that would leave the machine, then decides.
/// </summary>
internal sealed record AssistantBriefingResponse
{
    public required string Baseline { get; init; }

    public required DateTimeOffset CapturedAtUtc { get; init; }

    public required int BranchCount { get; init; }

    public required int OmittedBranchCount { get; init; }

    public required string Text { get; init; }
}
