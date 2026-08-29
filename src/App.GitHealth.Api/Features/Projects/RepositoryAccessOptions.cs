namespace App.GitHealth.Api.Features.Projects;

internal sealed class RepositoryAccessOptions
{
    public const string SectionName = "GitHealth";

    public string? InitialRepositoryPath { get; set; }

    public string? RepositoriesRoot { get; set; }
}
