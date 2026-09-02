using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace App.GitHealth.Api.Persistence;

/// <summary>
/// Used only by `dotnet ef` when it authors a migration. Without it the tool would boot the
/// application to find the context, and GitHealth's entry point opens a desktop window.
/// The in-memory connection string is never opened: only the model shape is read.
/// </summary>
internal sealed class GitHealthDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<GitHealthDbContext>
{
    public GitHealthDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<GitHealthDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options);
}
