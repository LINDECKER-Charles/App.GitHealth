using App.GitHealth.Core.Assistant;

namespace App.GitHealth.Api.Features.Assistant.Mcp;

/// <summary>
/// One capture, reachable by one token, for the length of one run. The token is the whole
/// authorisation: it names no project and carries no privilege of its own, so a bridge
/// request can only ever read back the capture the run was started against.
/// </summary>
internal sealed record AssistantMcpSession
{
    /// <summary>Secret drawn per run. Knowing it is the only way to reach the capture.</summary>
    public required string Token { get; init; }

    public required Guid RunId { get; init; }

    public required Guid ProjectId { get; init; }

    /// <summary>The measurements the tools serve. Nothing else is reachable through them.</summary>
    public required AnalysisBriefing Capture { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }
}
