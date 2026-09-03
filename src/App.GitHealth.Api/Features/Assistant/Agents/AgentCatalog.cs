using App.GitHealth.Core.Assistant;

namespace App.GitHealth.Api.Features.Assistant.Agents;

/// <summary>
/// The agents GitHealth knows how to drive. A CLI absent from this list is never launched:
/// running an arbitrary executable named by the interface is exactly what this feature must
/// not become.
/// </summary>
internal static class AgentCatalog
{
    /// <summary>
    /// Claude Code. <c>--tools ""</c> removes every built-in tool — no shell, no file read,
    /// no network — and <c>--allowedTools</c> then grants back only GitHealth's own, so the
    /// single thing this run can do is read the capture it was started for. That is a
    /// narrower grant than <c>--permission-mode plan</c>, which withholds edits but also
    /// refuses the bridge. <c>--strict-mcp-config</c> drops the machine's own servers, so
    /// the declaration passed here is the only one in force.
    /// <para>
    /// <c>stream-json</c> is asked for rather than plain text, and it is what makes a run
    /// watchable: the CLI narrates each turn as it happens instead of staying silent until
    /// it has an answer. <c>--verbose</c> and <c>--include-partial-messages</c> are the two
    /// flags that format requires — the first to be allowed at all under <c>--print</c>, the
    /// second to have the narration arrive while it is still worth reading.
    /// </para>
    /// </summary>
    private static readonly AgentDefinition Claude = new()
    {
        Id = "claude",
        DisplayName = "Claude Code",
        VersionArguments = ["--version"],
        RunArguments =
        [
            "--print",
            "--output-format", "stream-json",
            "--verbose",
            "--include-partial-messages",
            "--strict-mcp-config",
            "--mcp-config", AgentDefinition.BridgeConfigToken,
            "--tools", "",
            "--allowedTools", $"mcp__{AssistantPrompt.ToolNamespace}",
            AgentDefinition.EffortSlot,
        ],
        EffortArguments = ["--effort", AgentDefinition.EffortToken],
        Efforts = AgentEffort.All,
        DefaultEffort = AgentEffort.Medium,
        AnswerSource = AgentAnswerSource.EventStream,
        Events = AgentEventFormat.ClaudeStream,
        InstallationUrl = "https://claude.com/claude-code",
    };

    /// <summary>
    /// Codex CLI. <c>--sandbox read-only</c> is its own read-only policy;
    /// <c>--skip-git-repo-check</c> is required because the run happens in an empty scratch
    /// directory rather than in a repository, which is the point. The bridge and the effort
    /// both travel as configuration overrides, which is the only way this CLI exposes them —
    /// including the approval mode, without which it refuses its own tool call rather than
    /// asking a question no one is there to answer.
    /// <para>
    /// The whole <c>mcp_servers</c> table is replaced rather than added to, so the servers
    /// declared in the user's own configuration are not carried into the run. This is the
    /// nearest thing Codex has to Claude's <c>--strict-mcp-config</c>, and it is weaker:
    /// tools this CLI gets from its plugins and connectors stay reachable, and no flag
    /// removes them without also removing the credentials the run needs. GitHealth serves
    /// this agent one capture and nothing else, but it cannot promise that GitHealth is the
    /// only thing the agent can reach.
    /// </para>
    /// <para>
    /// <c>--json</c> turns its human log into the thread items the panel narrates a run
    /// with. The answer keeps coming from <c>--output-last-message</c>: that file is written
    /// by the CLI itself and is the one place the final message is stated as such.
    /// </para>
    /// </summary>
    private static readonly AgentDefinition Codex = new()
    {
        Id = "codex",
        DisplayName = "Codex CLI",
        VersionArguments = ["--version"],
        RunArguments =
        [
            "exec",
            "--json",
            AgentDefinition.EffortSlot,
            "-c", $"mcp_servers={{{AssistantPrompt.ToolNamespace}={{"
                + $"url=\"{AgentDefinition.BridgeUrlToken}\","
                + "default_tools_approval_mode=\"approve\"}}",
            "--sandbox", "read-only",
            "--skip-git-repo-check",
            "--color", "never",
            "--output-last-message", AgentDefinition.AnswerFileToken,
            "-",
        ],
        EffortArguments = ["-c", $"model_reasoning_effort={AgentDefinition.EffortToken}"],
        Efforts = AgentEffort.All,
        DefaultEffort = AgentEffort.Medium,
        AnswerSource = AgentAnswerSource.LastMessageFile,
        Events = AgentEventFormat.CodexItems,
        InstallationUrl = "https://developers.openai.com/codex/cli",
    };

    public static IReadOnlyList<AgentDefinition> All { get; } = [Claude, Codex];

    public static AgentDefinition? Find(string? id) => All.FirstOrDefault(
        agent => string.Equals(agent.Id, id, StringComparison.OrdinalIgnoreCase));
}
