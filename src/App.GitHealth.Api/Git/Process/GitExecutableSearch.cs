namespace App.GitHealth.Api.Git.Process;

/// <summary>
/// Emplacements interrogés pour localiser Git, capturés depuis l'environnement afin que la
/// résolution reste testable sans dépendre du poste qui exécute les tests.
/// </summary>
internal sealed record GitExecutableSearch
{
    private const string GitDirectoryName = "Git";
    private const string CommandDirectoryName = "cmd";
    private const string ProgramsDirectoryName = "Programs";
    private const string PathVariableName = "PATH";
    private const string WindowsExecutableName = "git.exe";
    private const string UnixExecutableName = "git";

    public required IReadOnlyList<string> ExecutableNames { get; init; }

    public required IReadOnlyList<string> PathDirectories { get; init; }

    public required IReadOnlyList<string> StandardLocations { get; init; }

    /// <summary>Sonde du système de fichiers, remplacée dans les tests.</summary>
    public Func<string, bool> FileExists { get; init; } = File.Exists;

    public static GitExecutableSearch Capture() => new()
    {
        ExecutableNames = OperatingSystem.IsWindows()
            ? [WindowsExecutableName, "git.cmd"]
            : [UnixExecutableName],
        PathDirectories = ReadPathDirectories(),
        StandardLocations = CaptureStandardLocations(),
    };

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

    private static string[] CaptureStandardLocations()
    {
        if (OperatingSystem.IsWindows())
        {
            return CaptureWindowsLocations();
        }

        return OperatingSystem.IsMacOS()
            ? ["/opt/homebrew/bin/git", "/usr/local/bin/git", "/usr/bin/git"]
            : ["/usr/bin/git", "/usr/local/bin/git"];
    }

    private static string[] CaptureWindowsLocations()
    {
        var installationRoots = new[]
        {
            SpecialDirectory(Environment.SpecialFolder.ProgramFiles),
            SpecialDirectory(Environment.SpecialFolder.ProgramFilesX86),
        };
        var locations = installationRoots
            .Where(root => root is not null)
            .Select(root => Path.Combine(root!, GitDirectoryName, CommandDirectoryName))
            .ToList();
        var userInstallationRoot = SpecialDirectory(
            Environment.SpecialFolder.LocalApplicationData);
        if (userInstallationRoot is not null)
        {
            locations.Add(Path.Combine(
                userInstallationRoot,
                ProgramsDirectoryName,
                GitDirectoryName,
                CommandDirectoryName));
        }

        return locations
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(directory => Path.Combine(directory, WindowsExecutableName))
            .ToArray();
    }

    private static string? SpecialDirectory(Environment.SpecialFolder folder)
    {
        var path = Environment.GetFolderPath(folder);
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }
}
