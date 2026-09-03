using App.GitHealth.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.GitHealth.Api.Persistence;

internal sealed class GitHealthDbContext(DbContextOptions<GitHealthDbContext> options)
    : DbContext(options)
{
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();

    public DbSet<ProjectBaselineEntity> ProjectBaselines => Set<ProjectBaselineEntity>();

    public DbSet<AnalysisRunEntity> AnalysisRuns => Set<AnalysisRunEntity>();

    public DbSet<BranchSnapshotEntity> BranchSnapshots => Set<BranchSnapshotEntity>();

    public DbSet<ContributorSnapshotEntity> ContributorSnapshots =>
        Set<ContributorSnapshotEntity>();

    public DbSet<AssistantConversationEntity> AssistantConversations =>
        Set<AssistantConversationEntity>();

    public DbSet<AssistantMessageEntity> AssistantMessages => Set<AssistantMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GitHealthDbContext).Assembly);
    }
}
