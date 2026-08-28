namespace App.GitHealth.Api.Persistence;

internal sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";
    public const int MinimumWriteTimeoutSeconds = 1;
    public const int MaximumWriteTimeoutSeconds = 60;

    public string DatabasePath { get; set; } = "data/githealth.db";

    public int WriteTimeoutSeconds { get; set; } = 5;

    public int? RetentionDays { get; set; }
}
