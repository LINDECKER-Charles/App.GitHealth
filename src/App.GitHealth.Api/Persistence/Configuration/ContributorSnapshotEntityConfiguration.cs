using App.GitHealth.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.GitHealth.Api.Persistence.Configuration;

internal sealed class ContributorSnapshotEntityConfiguration
    : IEntityTypeConfiguration<ContributorSnapshotEntity>
{
    private const int IdentityLength = 500;

    public void Configure(EntityTypeBuilder<ContributorSnapshotEntity> builder)
    {
        builder.ToTable("ContributorSnapshots");
        builder.HasKey(contributor => contributor.Id);
        builder.Property(contributor => contributor.Id).ValueGeneratedNever();
        builder.Property(contributor => contributor.Name).HasMaxLength(IdentityLength).IsRequired();
        builder.Property(contributor => contributor.Email)
            .HasMaxLength(IdentityLength)
            .IsRequired();
        builder.HasIndex(contributor => new
        {
            contributor.BranchSnapshotId,
            contributor.Name,
            contributor.Email,
        }).IsUnique();
    }
}
