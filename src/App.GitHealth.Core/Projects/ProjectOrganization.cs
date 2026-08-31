namespace App.GitHealth.Core.Projects;

/// <summary>
/// How a project is filed in the workspace: favourite flag and group membership.
/// This filing never enters the computation of an analysis, it only organises navigation.
/// </summary>
public sealed record ProjectOrganization
{
    public const int MaximumGroupNameLength = 60;

    private readonly string? _groupName;

    /// <summary>Project that is not a favourite and is filed nowhere.</summary>
    public static ProjectOrganization None { get; } = new();

    public bool IsFavorite { get; init; }

    /// <summary>Group name, normalised: an empty or blank label means "ungrouped".</summary>
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
                $"A group name cannot exceed {MaximumGroupNameLength} characters.",
                nameof(value));
        }

        return name;
    }
}
