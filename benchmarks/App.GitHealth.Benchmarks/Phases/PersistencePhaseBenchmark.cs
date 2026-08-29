using System.Text.Json;
using App.GitHealth.Api.Features.Snapshots;
using App.GitHealth.Api.Persistence;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;
using App.GitHealth.Core.Common;
using App.GitHealth.Core.Projects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace App.GitHealth.Benchmarks.Phases;

internal sealed class PersistencePhaseBenchmark : IAsyncDisposable
{
    private static readonly DateTimeOffset PersistedAt =
        new(2026, 8, 29, 12, 5, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);
    private readonly BenchmarkDbContextFactory _contextFactory;
    private readonly string _databaseRoot;
    private readonly IAnalysisRepository _analyses;
    private readonly IProjectRepository _projects;

    private PersistencePhaseBenchmark(
        string databaseRoot,
        PersistenceServices services,
        Guid projectId)
    {
        _databaseRoot = databaseRoot;
        _contextFactory = services.ContextFactory;
        _analyses = services.Analyses;
        _projects = services.Projects;
        ProjectId = projectId;
    }

    public Guid ProjectId { get; }

    public static async Task<PersistencePhaseBenchmark> CreateAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var databaseRoot = CreateDatabaseRoot();
        var contextFactory = CreateContextFactory(databaseRoot);
        await EnsureCreatedAsync(contextFactory, cancellationToken);
        var services = new PersistenceServices(
            contextFactory,
            new AnalysisRepository(contextFactory),
            new ProjectRepository(contextFactory));
        var project = CreateProject(repositoryPath);
        await services.Projects.AddAsync(project, PersistedAt, cancellationToken);
        return new PersistencePhaseBenchmark(databaseRoot, services, project.Id);
    }

    private static string CreateDatabaseRoot()
    {
        var databaseRoot = Path.Combine(
            Path.GetTempPath(),
            "githealth-benchmarks",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(databaseRoot);
        return databaseRoot;
    }

    private static BenchmarkDbContextFactory CreateContextFactory(string databaseRoot)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(databaseRoot, "benchmark.db"),
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = false,
        }.ToString();
        var options = new DbContextOptionsBuilder<GitHealthDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new BenchmarkDbContextFactory(options);
    }

    private static async Task EnsureCreatedAsync(
        BenchmarkDbContextFactory contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async Task<Guid> PersistAsync(
        RepositoryScan scan,
        CancellationToken cancellationToken)
    {
        var analysisId = await _analyses.StartAsync(
            ProjectId,
            PersistedAt,
            cancellationToken);
        var completion = new AnalysisCompletion(scan, PersistedAt.AddMinutes(1));
        await _analyses.CompleteAsync(analysisId, completion, cancellationToken);
        return analysisId;
    }

    public async Task<int> RenderApiPayloadAsync(
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        var mapper = new SnapshotMapper(new BenchmarkClock(PersistedAt));
        var service = new SnapshotService(_analyses, _projects, mapper);
        var response = await service.GetAnalysisPageAsync(
            analysisId,
            new SnapshotQueryParameters { PageSize = 200 },
            cancellationToken);
        if (!response.IsSuccess)
        {
            throw new InvalidOperationException(response.Failure!.Detail);
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(
            response.Value,
            SerializerOptions);
        return payload.Length;
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_databaseRoot))
        {
            Directory.Delete(_databaseRoot, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    private static Project CreateProject(string repositoryPath)
    {
        var project = Project.Create("Benchmark", repositoryPath);
        var settings = new ProjectSettings
        {
            Reference = new GitRef("refs/heads/main"),
            BranchNamespace = "refs/remotes/origin/benchmark/*",
            Thresholds = ActivityThresholds.Create(30, 90),
            Policy = BranchPolicy.Empty,
        };
        return project with { Settings = settings };
    }

    private sealed record BenchmarkClock(DateTimeOffset UtcNow) : IClock;

    private sealed record PersistenceServices(
        BenchmarkDbContextFactory ContextFactory,
        IAnalysisRepository Analyses,
        IProjectRepository Projects);
}

internal sealed class BenchmarkDbContextFactory(
    DbContextOptions<GitHealthDbContext> options)
    : IDbContextFactory<GitHealthDbContext>
{
    public GitHealthDbContext CreateDbContext() => new(options);

    public Task<GitHealthDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDbContext());
    }
}
