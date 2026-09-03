using System.Diagnostics;
using System.Text;
using App.GitHealth.Api.Features.Assistant.Mcp;

namespace App.GitHealth.Api.Features.Assistant.Agents;

/// <summary>
/// The exact command GitHealth is about to run, resolved from a catalog entry. Built as a
/// value so it can be asserted on in a test and shown in the interface: the user sees the
/// command line before they agree to it.
/// </summary>
internal sealed record AgentCommandLine
{
    private const string WindowsInterpreter = "cmd.exe";
    private const string RedactedToken = "<single-use-token>";
    private static readonly string[] ShimExtensions = [".cmd", ".bat"];

    public required string Executable { get; init; }

    public required IReadOnlyList<string> Arguments { get; init; }

    public static AgentCommandLine ForVersion(AgentLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return Create(location, location.Agent.VersionArguments);
    }

    public static AgentCommandLine ForRun(AgentLocation location, AgentRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(options);
        var agent = location.Agent;
        if (!AgentEffort.IsSupported(options.Effort, agent))
        {
            throw new ArgumentException(
                $"{agent.DisplayName} does not accept the \"{options.Effort}\" effort.",
                nameof(options));
        }

        return Create(location, [.. agent.RunArguments.SelectMany(
            argument => Materialise(argument, agent, options))]);
    }

    /// <summary>
    /// Expands one declared argument. The effort is a slot rather than a plain token because
    /// its position matters: Codex takes its overrides before the marker ending its command.
    /// </summary>
    private static IEnumerable<string> Materialise(
        string argument,
        AgentDefinition agent,
        AgentRunOptions options)
    {
        if (argument == AgentDefinition.EffortSlot)
        {
            return agent.EffortArguments.Select(effortArgument => effortArgument.Replace(
                AgentDefinition.EffortToken,
                options.Effort,
                StringComparison.Ordinal));
        }

        return [Substitute(argument, options)];
    }

    /// <summary>
    /// The bridge address sits inside a larger argument for one agent and stands alone for
    /// the other, so it is replaced in place rather than matched as a whole token.
    /// </summary>
    private static string Substitute(string argument, AgentRunOptions options)
    {
        if (argument == AgentDefinition.AnswerFileToken)
        {
            return options.AnswerFilePath;
        }

        if (argument == AgentDefinition.BridgeConfigToken)
        {
            return AssistantBridge.DescribeForClaude(options.BridgeAddress);
        }

        return argument.Replace(
            AgentDefinition.BridgeUrlToken,
            options.BridgeAddress.ToString(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The command as it is shown and kept, with this run's bridge token blanked. The shape
    /// of the command is what makes the feature auditable and is left whole; the token is a
    /// secret that outlives nothing, and storing it in an exportable database would be the
    /// one part of this line worth hiding.
    /// </summary>
    public string Describe(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return ToString().Replace(token, RedactedToken, StringComparison.Ordinal);
    }

    /// <summary>Reads back as it would be typed, for the interface and for a diagnostic.</summary>
    public override string ToString()
    {
        var builder = new StringBuilder(Quote(Executable));
        foreach (var argument in Arguments)
        {
            builder.Append(' ').Append(Quote(argument));
        }

        return builder.ToString();
    }

    public ProcessStartInfo CreateStartInfo(string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(Executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        foreach (var argument in Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        ConfigureEnvironment(startInfo.Environment);
        return startInfo;
    }

    /// <summary>
    /// The agent's own environment is left alone, unlike the one handed to Git: these CLIs
    /// read their credentials from it, and a scrubbed environment is a logged-out agent.
    /// Only the presentation is forced, so the trace stays free of escape sequences.
    /// </summary>
    private static void ConfigureEnvironment(IDictionary<string, string?> environment)
    {
        environment["NO_COLOR"] = "1";
        environment["FORCE_COLOR"] = "0";
        environment["TERM"] = "dumb";
    }

    /// <summary>
    /// A <c>.cmd</c> shim is not an executable image: <c>CreateProcess</c> refuses it, so it
    /// goes through the interpreter. npm installs the agent CLIs that way on Windows.
    /// </summary>
    private static AgentCommandLine Create(
        AgentLocation location,
        IReadOnlyList<string> arguments)
    {
        var path = location.ExecutablePath
            ?? throw new InvalidOperationException(location.UnavailableMessage);
        return NeedsInterpreter(path)
            ? new AgentCommandLine
            {
                Executable = WindowsInterpreter,
                Arguments = ["/c", path, .. arguments],
            }
            : new AgentCommandLine { Executable = path, Arguments = arguments };
    }

    private static bool NeedsInterpreter(string path) => OperatingSystem.IsWindows()
        && ShimExtensions.Contains(
            Path.GetExtension(path),
            StringComparer.OrdinalIgnoreCase);

    private static string Quote(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
}
