namespace App.GitHealth.Api.Features.Assistant.Agents;

/// <summary>
/// The agents GitHealth knows how to drive. A CLI absent from this list is never launched:
/// running an arbitrary executable named by the interface is exactly what this feature must
/// not become.
/// </summary>
internal static class AgentCatalog
{
    /// <summary>
    /// Claude Code. <c>--permission-mode plan</c> is the read-only mode: it can reason and
    /// answer, it cannot edit, write or run a mutation. <c>--strict-mcp-config</c> drops the
    /// machine's own MCP servers, so the run reaches nothing the user did not ask it to.
    /// </summary>
    private static readonly AgentDefinition Claude = new()
    {
        Id = "claude",
        DisplayName = "Claude Code",
        VersionArguments = ["--version"],
        RunArguments =
        [
            "--print",
            "--output-format", "text",
            "--permission-mode", "plan",
            "--strict-mcp-config",
        ],
        AnswerSource = AgentAnswerSource.StandardOutput,
        InstallationUrl = "https://claude.com/claude-code",
    };

    /// <summary>
    /// Codex CLI. <c>--sandbox read-only</c> is its own read-only policy;
    /// <c>--skip-git-repo-check</c> is required because the run happens in an empty scratch
    /// directory rather than in a repository, which is the point.
    /// </summary>
    private static readonly AgentDefinition Codex = new()
    {
        Id = "codex",
        DisplayName = "Codex CLI",
        VersionArguments = ["--version"],
        RunArguments =
        [
            "exec",
            "--sandbox", "read-only",
            "--skip-git-repo-check",
            "--color", "never",
            "--output-last-message", AgentDefinition.AnswerFileToken,
            "-",
        ],
        AnswerSource = AgentAnswerSource.LastMessageFile,
        InstallationUrl = "https://developers.openai.com/codex/cli",
    };

    public static IReadOnlyList<AgentDefinition> All { get; } = [Claude, Codex];

    public static AgentDefinition? Find(string? id) => All.FirstOrDefault(
        agent => string.Equals(agent.Id, id, StringComparison.OrdinalIgnoreCase));
}
