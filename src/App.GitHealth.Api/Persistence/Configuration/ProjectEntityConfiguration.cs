using App.GitHealth.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.GitHealth.Api.Persistence.Configuration;

internal sealed class ProjectEntityConfiguration : IEntityTypeConfiguration<ProjectEntity>
{
    private const int NameLength = 200;
    private const int PathLength = 2048;
    private const int RefLength = 1024;

    public void Configure(EntityTypeBuilder<ProjectEntity> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(project => project.Id);
        builder.Property(project => project.Id).ValueGeneratedNever();
        builder.Property(project => project.DisplayName).HasMaxLength(NameLength).IsRequired();
        builder.Property(project => project.RepositoryPath).HasMaxLength(PathLength).IsRequired();
        builder.HasIndex(project => project.RepositoryPath).IsUnique();
        builder.Property(project => project.ReferenceName).HasMaxLength(RefLength);
        builder.Property(project => project.BranchNamespace).HasMaxLength(RefLength).IsRequired();
        builder.Property(project => project.ExcludedPatternsJson).IsRequired();
        builder.Property(project => project.ProtectedPatternsJson).IsRequired();
        builder.Property(project => project.CreatedAtUtc)
            .HasConversion<UtcDateTimeOffsetConverter>();
        builder.Property(project => project.UpdatedAtUtc)
            .HasConversion<UtcDateTimeOffsetConverter>();
        builder.HasIndex(project => project.LastSuccessfulAnalysisId);
        builder.HasMany(project => project.AnalysisRuns)
            .WithOne(analysis => analysis.Project)
            .HasForeignKey(analysis => analysis.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
