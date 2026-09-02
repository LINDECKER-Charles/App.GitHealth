namespace App.GitHealth.Api.Features.Assistant.Agents;

/// <summary>
/// Places an agent CLI is looked for, captured from the environment so resolution stays
/// testable without depending on the machine running the tests.
/// </summary>
/// <remarks>
/// The standard directories carry most of the weight here, more than they do for Git. A
/// desktop process started from the Finder or the Explorer inherits the system's minimal
/// <c>PATH</c>, not the one the user's shell builds — so the CLI they installed last week
/// and run every day is very often absent from <c>PATH</c> as this process sees it.
/// </remarks>
internal sealed record AgentExecutableSearch
{
    private const string PathVariableName = "PATH";

    /// <summary>Appended to the agent's identifier: on Windows a CLI is rarely extensionless.</summary>
    public required IReadOnlyList<string> ExecutableSuffixes { get; init; }

    public required IReadOnlyList<string> PathDirectories { get; init; }

    public required IReadOnlyList<string> StandardDirectories { get; init; }

    /// <summary>File system probe, replaced in the tests.</summary>
    public Func<string, bool> FileExists { get; init; } = File.Exists;

    public static AgentExecutableSearch Capture() => new()
    {
        // A real executable comes first: a .cmd shim has to be run through the command
        // interpreter, which is one more layer between GitHealth and the agent.
        ExecutableSuffixes = OperatingSystem.IsWindows()
            ? [".exe", ".cmd", ".bat"]
            : [string.Empty],
        PathDirectories = ReadPathDirectories(),
        StandardDirectories = CaptureStandardDirectories(),
    };

    /// <summary>
    /// Directories tried for one agent: the shared ones, then the private installation
    /// directory the tool keeps under the home folder — Claude Code's own installer uses it.
    /// </summary>
    public IEnumerable<string> DirectoriesFor(AgentDefinition agent)
    {
        var privateDirectory = Home() is { } home
            ? Path.Combine(home, $".{agent.Id}", "local")
            : null;
        return privateDirectory is null
            ? StandardDirectories
            : StandardDirectories.Append(privateDirectory);
    }

    private static string[] ReadPathDirectories()
    {
        var variable = Environment.GetEnvironmentVariable(PathVariableName);
        return string.IsNullOrWhiteSpace(variable)
            ? []
            : variable
                .Split(
                    Path.PathSeparator,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(directory => directory.Trim('"'))
                .Where(directory => directory.Length > 0)
                .ToArray();
    }

    private static string[] CaptureStandardDirectories()
    {
        var shared = OperatingSystem.IsWindows()
            ? WindowsDirectories()
            : UnixDirectories();
        return [.. shared.Concat(UserDirectories()).Distinct(StringComparer.Ordinal)];
    }

    private static IEnumerable<string> WindowsDirectories()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return string.IsNullOrWhiteSpace(roaming) ? [] : [Path.Combine(roaming, "npm")];
    }

    private static IEnumerable<string> UnixDirectories() => OperatingSystem.IsMacOS()
        ? ["/opt/homebrew/bin", "/usr/local/bin", "/usr/bin"]
        : ["/usr/local/bin", "/usr/bin"];

    /// <summary>Where a per-user install lands, whichever package manager put it there.</summary>
    private static IEnumerable<string> UserDirectories()
    {
        if (Home() is not { } home)
        {
            return [];
        }

        return
        [
            Path.Combine(home, ".local", "bin"),
            Path.Combine(home, ".bun", "bin"),
            Path.Combine(home, ".volta", "bin"),
        ];
    }

    private static string? Home()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(home) ? null : home;
    }
}
