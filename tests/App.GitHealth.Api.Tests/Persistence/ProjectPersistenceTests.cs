using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Core.Branches;
using App.GitHealth.Core.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace App.GitHealth.Api.Tests.Persistence;

public sealed class ProjectPersistenceTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProjectSettingsSurviveRestart()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var path = Path.Combine(database.RootPath, "repository");
        var project = PersistenceTestData.CreateProject(path);
        await AddProjectAsync(database, project, CreatedAt);
        var settings = project.Settings with
        {
            Reference = new GitRef("refs/remotes/origin/main"),
            Thresholds = ActivityThresholds.Create(7, 45),
        };
        await UpdateSettingsAsync(database, project.Id, settings);

        await using var reopened = await database.ReopenAsync();
        await using var scope = reopened.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
            .GetAsync(project.Id, CancellationToken.None);

        Assert.NotNull(stored);
        var restored = stored.ToDomain();
        Assert.Equal("refs/remotes/origin/main", restored.Settings.Reference!.FullName);
        Assert.Equal("refs/remotes/origin/*", restored.Settings.BranchNamespace);
        Assert.Equal(7, restored.Settings.Thresholds.ActiveUntilDays);
        Assert.Equal(["refs/heads/tmp/*"], restored.Settings.Policy.ExcludedPatterns);
        Assert.Equal(TimeSpan.Zero, stored.CreatedAtUtc.Offset);
    }

    [Fact]
    public async Task RelocationPreservesProjectAndHistoricalAnalyses()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var project = PersistenceTestData.CreateProject(Path.Combine(database.RootPath, "old"));
        await AddProjectAsync(database, project, CreatedAt);
        var analysisId = await CompleteAnalysisAsync(database, project.Id);
        var newPath = Path.Combine(database.RootPath, "relocated");

        await RelocateAsync(database, project.Id, newPath);

        await using var scope = database.CreateScope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var analyses = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var stored = await projects.GetAsync(project.Id, CancellationToken.None);
        var last = await analyses.GetLastSuccessfulAsync(project.Id, CancellationToken.None);
        Assert.Equal(Path.GetFullPath(newPath), stored!.RepositoryPath);
        Assert.True(stored.IsRepositoryAccessible);
        Assert.Equal(analysisId, last!.Id);
    }

    private static async Task AddProjectAsync(
        SqliteTestDatabase database,
        App.GitHealth.Core.Projects.Project project,
        DateTimeOffset createdAt)
    {
        await using var scope = database.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
            .AddAsync(project, createdAt, CancellationToken.None);
    }

    private static async Task<Guid> CompleteAnalysisAsync(
        SqliteTestDatabase database,
        Guid projectId)
    {
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var capturedAt = CreatedAt.AddHours(1);
        var analysisId = await repository.StartAsync(projectId, CreatedAt, CancellationToken.None);
        var completion = new AnalysisCompletion(
            PersistenceTestData.CreateScan(capturedAt),
            capturedAt.AddMinutes(1));
        await repository.CompleteAsync(analysisId, completion, CancellationToken.None);
        return analysisId;
    }

    private static async Task RelocateAsync(
        SqliteTestDatabase database,
        Guid projectId,
        string newPath)
    {
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await repository.MarkUnavailableAsync(
            projectId,
            CreatedAt.AddDays(1),
            CancellationToken.None);
        var relocation = new ProjectRelocation(projectId, newPath, CreatedAt.AddDays(2));
        await repository.RelocateAsync(relocation, CancellationToken.None);
    }

    private static async Task UpdateSettingsAsync(
        SqliteTestDatabase database,
        Guid projectId,
        ProjectSettings settings)
    {
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var update = new ProjectSettingsUpdate(projectId, settings, CreatedAt.AddHours(1));
        await repository.UpdateSettingsAsync(update, CancellationToken.None);
    }
}
