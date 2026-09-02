using System.Diagnostics;
using System.Text;

namespace App.GitHealth.Api.Features.Assistant.Agents;

/// <summary>
/// The exact command GitHealth is about to run, resolved from a catalog entry. Built as a
/// value so it can be asserted on in a test and shown in the interface: the user sees the
/// command line before they agree to it.
/// </summary>
internal sealed record AgentCommandLine
{
    private const string WindowsInterpreter = "cmd.exe";
    private static readonly string[] ShimExtensions = [".cmd", ".bat"];

    public required string Executable { get; init; }

    public required IReadOnlyList<string> Arguments { get; init; }

    public static AgentCommandLine ForVersion(AgentLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return Create(location, location.Agent.VersionArguments);
    }

    public static AgentCommandLine ForRun(AgentLocation location, string answerFilePath)
    {
        ArgumentNullException.ThrowIfNull(location);
        var arguments = location.Agent.RunArguments
            .Select(argument => argument == AgentDefinition.AnswerFileToken
                ? answerFilePath
                : argument)
            .ToArray();
        return Create(location, arguments);
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
