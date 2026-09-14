namespace App.GitHealth.Api.Hosting.Desktop.Bridge;

/// <summary>
/// Writes to disk a file the page hands over.
/// </summary>
/// <remarks>
/// A webview download never lands: Photino declares no <c>WKDownloadDelegate</c> on macOS
/// and no download handler on Linux, so WebKit turns an anchor carrying <c>download</c>
/// into a download nobody ever gives a destination to, and drops it. The file goes where a
/// browser would have put it — the downloads folder — and the host answers with the path,
/// so the page can say where it landed.
/// </remarks>
internal static class DesktopFileSaver
{
    public const string Kind = "saveFile";

    private const string DownloadsFolderName = "Downloads";

    /// <summary>Past that many files of one name, the save is refused rather than silent.</summary>
    private const int MaxNameAttempts = 100;

    private static readonly char[] ForbiddenNameChars =
        [.. Path.GetInvalidFileNameChars(), '/', '\\'];

    public static DesktopBridgeReply Handle(DesktopBridgeRequest request, string id)
    {
        ArgumentNullException.ThrowIfNull(request);
        var path = Save(ResolveDownloadsFolder(), request.FileName, request.Contents);
        return new DesktopBridgeReply
        {
            Id = id,
            Kind = Kind,
            Path = path,
            IsHandled = path is not null,
        };
    }

    /// <returns>The path written, or <see langword="null" /> when nothing was.</returns>
    internal static string? Save(string directory, string? fileName, string? contents)
    {
        if (ReadName(fileName) is not { } name || Decode(contents) is not { } payload)
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(directory);
            var path = FreePath(directory, name);
            if (path is not null)
            {
                File.WriteAllBytes(path, payload);
            }

            return path;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // A refused write must not kill the window: the page reports the failure.
            Console.Error.WriteLine($"The file could not be saved: {exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// Where a browser would have put the file. No <see cref="Environment.SpecialFolder" />
    /// names it on any platform, so it is read from the home directory.
    /// </summary>
    private static string ResolveDownloadsFolder()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home.Length == 0 ? Path.GetTempPath() : home, DownloadsFolderName);
    }

    /// <summary>
    /// The name comes from the page: every separator is turned away, so no payload writes
    /// itself outside the downloads folder.
    /// </summary>
    private static string? ReadName(string? fileName)
    {
        var name = fileName?.Trim();
        return string.IsNullOrEmpty(name)
            || name is "." or ".."
            || name.IndexOfAny(ForbiddenNameChars) >= 0
            ? null
            : name;
    }

    /// <summary>
    /// A second export does not overwrite the first: the name gains a counter, the way a
    /// browser numbers a download it has already seen.
    /// </summary>
    private static string? FreePath(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        if (!File.Exists(path))
        {
            return path;
        }

        var stem = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        for (var counter = 2; counter <= MaxNameAttempts; counter++)
        {
            path = Path.Combine(directory, $"{stem} ({counter}){extension}");
            if (!File.Exists(path))
            {
                return path;
            }
        }

        Console.Error.WriteLine($"Too many files already named {name} in {directory}.");
        return null;
    }

    /// <summary>The bridge carries text: the file travels base64-encoded.</summary>
    private static byte[]? Decode(string? contents)
    {
        if (contents is null)
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(contents);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
