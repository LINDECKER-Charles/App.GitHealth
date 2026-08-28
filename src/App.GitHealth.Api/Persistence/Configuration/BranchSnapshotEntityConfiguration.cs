using App.GitHealth.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.GitHealth.Api.Persistence.Configuration;

internal sealed class BranchSnapshotEntityConfiguration
    : IEntityTypeConfiguration<BranchSnapshotEntity>
{
    private const int RefLength = 1024;
    private const int CommitLength = 128;
    private const int AuthorLength = 500;

    public void Configure(EntityTypeBuilder<BranchSnapshotEntity> builder)
    {
        builder.ToTable("BranchSnapshots");
        builder.HasKey(branch => branch.Id);
        builder.Property(branch => branch.Id).ValueGeneratedNever();
        builder.Property(branch => branch.ReferenceName).HasMaxLength(RefLength).IsRequired();
        builder.Property(branch => branch.CommitId).HasMaxLength(CommitLength).IsRequired();
        builder.Property(branch => branch.Relationship).HasConversion<string>().HasMaxLength(40);
        builder.Property(branch => branch.TipAuthor).HasMaxLength(AuthorLength);
        builder.Property(branch => branch.LastActivityAtUtc)
            .HasConversion<NullableUtcDateTimeOffsetConverter>();
        builder.HasIndex(branch => new { branch.AnalysisRunId, branch.ReferenceName }).IsUnique();
        builder.HasMany(branch => branch.Contributors)
            .WithOne(contributor => contributor.BranchSnapshot)
            .HasForeignKey(contributor => contributor.BranchSnapshotId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
