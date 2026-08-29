namespace App.GitHealth.Core.Analysis;

/// <summary>Describes the immutable environment captured for one repository scan.</summary>
public sealed record RepositoryScanMetadata
{
    public RepositoryScanMetadata(DateTimeOffset capturedAt, string gitVersion)
    {
        if (capturedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "La date de capture doit être en UTC.",
                nameof(capturedAt));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(gitVersion);
        CapturedAt = capturedAt;
        GitVersion = gitVersion.Trim();
    }

    public DateTimeOffset CapturedAt { get; }

    public string GitVersion { get; }
}
