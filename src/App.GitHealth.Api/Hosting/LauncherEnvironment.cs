namespace App.GitHealth.Api.Hosting;

internal sealed record LauncherEnvironment
{
    public required RuntimePlatform Platform { get; init; }

    public required string CurrentDirectory { get; init; }

    public required string UserProfilePath { get; init; }

    public string? LocalApplicationDataPath { get; init; }

    public string? XdgDataHomePath { get; init; }

    public static LauncherEnvironment Capture()
    {
        var currentDirectory = Environment.CurrentDirectory;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            userProfile = Environment.GetEnvironmentVariable("HOME") ?? currentDirectory;
        }

        return new LauncherEnvironment
        {
            Platform = DetectPlatform(),
            CurrentDirectory = currentDirectory,
            UserProfilePath = userProfile,
            LocalApplicationDataPath = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            XdgDataHomePath = Environment.GetEnvironmentVariable("XDG_DATA_HOME"),
        };
    }

    private static RuntimePlatform DetectPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return RuntimePlatform.Windows;
        }

        if (OperatingSystem.IsMacOS())
        {
            return RuntimePlatform.MacOS;
        }

        return OperatingSystem.IsLinux()
            ? RuntimePlatform.Linux
            : throw new PlatformNotSupportedException(
                "GitHealth supports Windows, macOS and Linux.");
    }
}
