namespace App.GitHealth.Api.Features.Assistant.Agents;

/// <summary>Where the agent's actual answer is found once the process has exited.</summary>
internal enum AgentAnswerSource
{
    /// <summary>Standard output carries the answer and nothing else.</summary>
    StandardOutput,

    /// <summary>Standard output is a running trace; the answer lands in a file we name.</summary>
    LastMessageFile,
}

/// <summary>
/// One supported command-line agent, described as data. Every flag here exists to make the
/// run read-only and non-interactive — GitHealth invokes somebody else's tool, so it says
/// exactly what it is asking for rather than trusting a default to stay put.
/// </summary>
internal sealed record AgentDefinition
{
    /// <summary>Replaced by the run's answer file when the arguments are materialised.</summary>
    public const string AnswerFileToken = "{answerFile}";

    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Arguments that print the version, used to prove the executable answers.</summary>
    public required IReadOnlyList<string> VersionArguments { get; init; }

    /// <summary>
    /// Arguments of a run. The prompt is never among them: it travels on standard input,
    /// where no command-line length limit can truncate a briefing.
    /// </summary>
    public required IReadOnlyList<string> RunArguments { get; init; }

    public required AgentAnswerSource AnswerSource { get; init; }

    public required string InstallationUrl { get; init; }

    /// <summary>Configuration key overriding the search, mirroring the one Git already has.</summary>
    public string ConfigurationKey => $"GitHealth:Assistant:Agents:{Id}:ExecutablePath";
}
