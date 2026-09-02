namespace App.GitHealth.Api.Persistence.Entities;

/// <summary>
/// One comparison baseline of a project. Its identity is the reference name, so reordering
/// the list keeps each baseline's own history pointer attached to it.
/// </summary>
internal sealed class ProjectBaselineEntity
{
    private ProjectBaselineEntity()
    {
    }

    public Guid ProjectId { get; private set; }

    public ProjectEntity Project { get; private set; } = null!;

    public string ReferenceName { get; private set; } = string.Empty;

    /// <summary>Display and scan order. Position 0 is the primary baseline.</summary>
    public int Position { get; private set; }

    /// <summary>Newest completed run for this baseline. No foreign key, like Projects.</summary>
    public Guid? LastSuccessfulAnalysisId { get; set; }

    public static ProjectBaselineEntity Create(
        Guid projectId,
        string referenceName,
        int position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceName);
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        return new ProjectBaselineEntity
        {
            ProjectId = projectId,
            ReferenceName = referenceName,
            Position = position,
        };
    }

    public void MoveTo(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        Position = position;
    }
}
