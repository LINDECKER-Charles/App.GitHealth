namespace App.GitHealth.Api.Persistence.Services;

internal static class PrivateFilePermissions
{
    private const UnixFileMode DirectoryMode = UnixFileMode.UserRead
        | UnixFileMode.UserWrite
        | UnixFileMode.UserExecute;
    private const UnixFileMode FileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public static void EnsureDirectory(string path)
    {
        var alreadyExists = Directory.Exists(path);
        Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows() && !alreadyExists)
        {
            File.SetUnixFileMode(path, DirectoryMode);
        }
    }

    public static void CreateFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using var stream = File.Open(
                path,
                System.IO.FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None);
            return;
        }

        using var privateStream = new FileStream(path, new FileStreamOptions
        {
            Access = FileAccess.ReadWrite,
            Mode = System.IO.FileMode.CreateNew,
            Share = FileShare.None,
            UnixCreateMode = FileMode,
        });
    }

    public static void EnsureFile(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, FileMode);
        }
    }

    public static string CreateTemporaryDirectory(string prefix)
    {
        var path = Directory.CreateTempSubdirectory(prefix).FullName;
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, DirectoryMode);
        }

        return path;
    }

    public static FileStream OpenOrCreateExclusive(string path)
    {
        var options = new FileStreamOptions
        {
            Access = FileAccess.ReadWrite,
            Mode = System.IO.FileMode.OpenOrCreate,
            Share = FileShare.None,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = FileMode;
        }

        var stream = new FileStream(path, options);
        EnsureFile(path);
        return stream;
    }
}
