namespace App.GitHealth.Core.Projects;

/// <summary>
/// Rangement d'un projet dans l'espace de travail : mise en favori et groupe d'appartenance.
/// Ce classement n'entre jamais dans le calcul d'une analyse, il n'organise que la navigation.
/// </summary>
public sealed record ProjectOrganization
{
    public const int MaximumGroupNameLength = 60;

    private readonly string? _groupName;

    /// <summary>Projet non favori et rangé nulle part.</summary>
    public static ProjectOrganization None { get; } = new();

    public bool IsFavorite { get; init; }

    /// <summary>Nom du groupe, normalisé : un libellé vide ou blanc vaut « sans groupe ».</summary>
    public string? GroupName
    {
        get => _groupName;
        init => _groupName = Normalize(value);
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var name = value.Trim();
        if (name.Length > MaximumGroupNameLength)
        {
            throw new ArgumentException(
                $"Le nom d’un groupe ne peut pas dépasser {MaximumGroupNameLength} caractères.",
                nameof(value));
        }

        return name;
    }
}
