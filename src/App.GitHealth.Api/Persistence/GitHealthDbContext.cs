using App.GitHealth.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.GitHealth.Api.Persistence;

internal sealed class GitHealthDbContext(DbContextOptions<GitHealthDbContext> options)
    : DbContext(options)
{
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();

    public DbSet<AnalysisRunEntity> AnalysisRuns => Set<AnalysisRunEntity>();

    public DbSet<BranchSnapshotEntity> BranchSnapshots => Set<BranchSnapshotEntity>();

    public DbSet<ContributorSnapshotEntity> ContributorSnapshots =>
        Set<ContributorSnapshotEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GitHealthDbContext).Assembly);
    }
}
