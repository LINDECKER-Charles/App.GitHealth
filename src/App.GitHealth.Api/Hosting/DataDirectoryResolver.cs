namespace App.GitHealth.Api.Hosting;

internal sealed class DataDirectoryResolver
{
    private const string ApplicationDirectoryName = "GitHealth";
    private readonly LauncherEnvironment _environment;

    public DataDirectoryResolver(LauncherEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment.CurrentDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment.UserProfilePath);
        _environment = environment;
    }

    public static DataDirectoryResolver ForCurrentPlatform() => new(
        LauncherEnvironment.Capture());

    public string Resolve(string? configuredDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return Path.GetFullPath(configuredDirectory, _environment.CurrentDirectory);
        }

        return _environment.Platform switch
        {
            RuntimePlatform.Windows => ResolveWindowsDefault(),
            RuntimePlatform.MacOS => CombineUnix(
                _environment.UserProfilePath,
                "Library/Application Support",
                ApplicationDirectoryName),
            RuntimePlatform.Linux => ResolveLinuxDefault(),
            _ => throw new PlatformNotSupportedException(),
        };
    }

    private string ResolveWindowsDefault()
    {
        var baseDirectory = _environment.LocalApplicationDataPath;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = CombineWindows(
                _environment.UserProfilePath,
                "AppData",
                "Local");
        }

        return CombineWindows(baseDirectory, ApplicationDirectoryName);
    }

    private string ResolveLinuxDefault()
    {
        var xdgDataHome = _environment.XdgDataHomePath;
        return !string.IsNullOrWhiteSpace(xdgDataHome) && xdgDataHome.StartsWith('/')
            ? CombineUnix(xdgDataHome, ApplicationDirectoryName)
            : CombineUnix(
                _environment.UserProfilePath,
                ".local/share",
                ApplicationDirectoryName);
    }

    private static string CombineWindows(params string[] segments) =>
        Combine('\\', segments);

    private static string CombineUnix(params string[] segments) => Combine('/', segments);

    private static string Combine(char separator, string[] segments)
    {
        var result = segments[0].TrimEnd('\\', '/');
        for (var index = 1; index < segments.Length; index++)
        {
            result = string.Concat(
                result,
                separator,
                segments[index].Trim('\\', '/'));
        }

        return result;
    }
}
