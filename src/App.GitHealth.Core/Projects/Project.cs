namespace App.GitHealth.Core.Projects;

public sealed record Project
{
    private Project(Guid id, string displayName, string repositoryPath)
    {
        Id = id;
        DisplayName = displayName;
        RepositoryPath = repositoryPath;
    }

    public Guid Id { get; }

    public string DisplayName { get; }

    public string RepositoryPath { get; }

    public ProjectSettings Settings { get; init; } = ProjectSettings.Default;

    public static Project Create(string displayName, string repositoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        return new Project(Guid.NewGuid(), displayName.Trim(), repositoryPath);
    }

    public static Project Restore(Guid id, string displayName, string repositoryPath)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L’identifiant du projet est obligatoire.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        return new Project(id, displayName.Trim(), repositoryPath);
    }
}
