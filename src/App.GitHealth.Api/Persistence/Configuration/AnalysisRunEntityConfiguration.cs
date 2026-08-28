using App.GitHealth.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.GitHealth.Api.Persistence.Configuration;

internal sealed class AnalysisRunEntityConfiguration
    : IEntityTypeConfiguration<AnalysisRunEntity>
{
    private const int RefLength = 1024;
    private const int CommitLength = 128;
    private const int VersionLength = 100;
    private const int FailureCodeLength = 100;
    private const int FailureMessageLength = 2000;

    public void Configure(EntityTypeBuilder<AnalysisRunEntity> builder)
    {
        builder.ToTable("AnalysisRuns");
        builder.HasKey(analysis => analysis.Id);
        builder.Property(analysis => analysis.Id).ValueGeneratedNever();
        builder.Property(analysis => analysis.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(analysis => analysis.StartedAtUtc)
            .HasConversion<UtcDateTimeOffsetConverter>();
        builder.Property(analysis => analysis.CompletedAtUtc)
            .HasConversion<NullableUtcDateTimeOffsetConverter>();
        builder.Property(analysis => analysis.CapturedAtUtc)
            .HasConversion<NullableUtcDateTimeOffsetConverter>();
        ConfigureStrings(builder);
        builder.HasIndex(analysis => new { analysis.ProjectId, analysis.StartedAtUtc });
        builder.HasMany(analysis => analysis.Branches)
            .WithOne(branch => branch.AnalysisRun)
            .HasForeignKey(branch => branch.AnalysisRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureStrings(EntityTypeBuilder<AnalysisRunEntity> builder)
    {
        builder.Property(analysis => analysis.ReferenceName).HasMaxLength(RefLength).IsRequired();
        builder.Property(analysis => analysis.ReferenceCommit).HasMaxLength(CommitLength);
        builder.Property(analysis => analysis.BranchNamespace).HasMaxLength(RefLength).IsRequired();
        builder.Property(analysis => analysis.GitVersion).HasMaxLength(VersionLength);
        builder.Property(analysis => analysis.FailureCode).HasMaxLength(FailureCodeLength);
        builder.Property(analysis => analysis.FailureMessage).HasMaxLength(FailureMessageLength);
        builder.Property(analysis => analysis.ExcludedPatternsJson).IsRequired();
        builder.Property(analysis => analysis.ProtectedPatternsJson).IsRequired();
    }
}
