using App.GitHealth.Api.Persistence.Services;

namespace App.GitHealth.Api.Features.Assistant;

/// <summary>
/// The empty directory a run happens in. Deliberately not the analysed repository: an agent
/// started there could read it, and could be asked to write to it. Started here, the
/// strongest guarantee GitHealth makes — it touches nothing in your repository — survives
/// the fact that the process running is somebody else's.
/// </summary>
internal sealed class AssistantScratch : IDisposable
{
    private const string Prefix = "githealth-assistant-";
    private const string AnswerFileName = "answer.md";

    private AssistantScratch(string directory)
    {
        Directory = directory;
        AnswerFilePath = Path.Combine(directory, AnswerFileName);
    }

    public string Directory { get; }

    /// <summary>Where an agent that reports through a file is told to write its answer.</summary>
    public string AnswerFilePath { get; }

    public static AssistantScratch Create() =>
        new(PrivateFilePermissions.CreateTemporaryDirectory(Prefix));

    /// <summary>Reads what the agent left, or nothing when it never got that far.</summary>
    public string? ReadAnswer()
    {
        try
        {
            var content = File.Exists(AnswerFilePath)
                ? File.ReadAllText(AnswerFilePath).Trim()
                : string.Empty;
            return content.Length == 0 ? null : content;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        try
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            // A scratch directory left behind is the operating system's to reclaim; the
            // run itself already produced its answer and must not fail on the cleanup.
        }
    }
}
