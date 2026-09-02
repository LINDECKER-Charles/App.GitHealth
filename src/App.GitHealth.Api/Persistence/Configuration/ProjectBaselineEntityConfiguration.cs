using App.GitHealth.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.GitHealth.Api.Persistence.Configuration;

internal sealed class ProjectBaselineEntityConfiguration
    : IEntityTypeConfiguration<ProjectBaselineEntity>
{
    private const int RefLength = 1024;

    public void Configure(EntityTypeBuilder<ProjectBaselineEntity> builder)
    {
        builder.ToTable("ProjectBaselines");
        builder.HasKey(baseline => new { baseline.ProjectId, baseline.ReferenceName });
        builder.Property(baseline => baseline.ReferenceName)
            .HasMaxLength(RefLength)
            .IsRequired();
        builder.HasIndex(baseline => new { baseline.ProjectId, baseline.Position });
        builder.HasIndex(baseline => baseline.LastSuccessfulAnalysisId);
    }
}
