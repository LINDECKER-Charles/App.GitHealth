using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;
using App.GitHealth.Core.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.GitHealth.Api.Tests.Snapshots;

public sealed class SnapshotEndpointTests
{
    private const string BranchAuthor = "Ada Lovelace";
    private const string SecondaryBaseline = "refs/heads/release";
    private const string SecondaryBranch = "release";

    [Fact]
    public async Task SnapshotsArePagedWithStableCursorAndExposeContributors()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 5);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var run = await AnalyzeAsync(client, repository.RepositoryPath);
        await RemoveCurrentPoliciesAsync(client, run.ProjectId);

        var first = await GetPageAsync(client, run.ProjectId, cursor: null);
        Assert.Equal(run.AnalysisId, first.GetProperty("analysisId").GetGuid());
        var firstItems = first.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, firstItems.Length);
        Assert.All(firstItems, item => Assert.False(item.GetProperty("isProtected").GetBoolean()));
        var cursor = first.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));

        var second = await GetPageAsync(client, run.ProjectId, cursor);
        var secondItems = second.GetProperty("items").EnumerateArray().ToArray();
        Assert.DoesNotContain(
            secondItems,
            item => firstItems.Any(firstItem => SnapshotId(firstItem) == SnapshotId(item)));
        await AssertChangedFilterIsRejectedAsync(client, run.ProjectId, cursor!);
        await AssertDetailAsync(client, firstItems[0]);
    }

    [Fact]
    public async Task AttributionIsUnavailableOnlyForMergedBranchesWithoutOwnCommits()
    {
        using var repository = GitTestRepository.Create();
        repository.AddSynchronizedBranch("feature/synchronized");
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var run = await AnalyzeAsync(client, repository.RepositoryPath);

        var merged = await GetSingleSnapshotAsync(client, run.ProjectId, "behind");
        var synchronized = await GetSingleSnapshotAsync(
            client,
            run.ProjectId,
            "synchronized");

        await AssertAttributionStatusAsync(client, merged, "UnavailableAfterMerge");
        await AssertAttributionStatusAsync(client, synchronized, "Available");
    }

    [Fact]
    public async Task HistoricalActivityUsesCaptureTimeWhileLatestUsesCurrentTime()
    {
        using var repository = GitTestRepository.Create();
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        using var factory = new ApiApplicationFactory
        {
            RepositoriesRoot = repository.RootPath,
            TestServices = services =>
            {
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(clock);
            },
        };
        using var client = factory.CreateClient();
        var run = await AnalyzeAsync(client, repository.RepositoryPath);
        clock.UtcNow = clock.UtcNow.AddDays(120);

        var latest = await GetSingleSnapshotAsync(client, run.ProjectId, "near-00");
        var historical = await GetAnalysisSnapshotAsync(client, run.AnalysisId, "near-00");
        Assert.Equal("Inactive", latest.GetProperty("activity").GetString());
        Assert.Equal("Active", historical.GetProperty("activity").GetString());

        var detail = await client.GetFromJsonAsync<JsonElement>(
            $"/api/branch-snapshots/{SnapshotId(historical)}");
        Assert.Equal(
            "Active",
            detail.GetProperty("snapshot").GetProperty("activity").GetString());
    }

    [Fact]
    public async Task FailedScanKeepsLastSuccessfulSnapshotAvailable()
    {
        using var repository = GitTestRepository.Create();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var run = await AnalyzeAsync(client, repository.RepositoryPath);

        repository.DeleteRepository();
        var failed = await ApiTestWorkflow.LaunchAsync(client, run.ProjectId);
        using var failedResponse = failed.Response;
        Assert.Equal(HttpStatusCode.Accepted, failedResponse.StatusCode);
        await ApiTestWorkflow.WaitForStatusAsync(client, failed.Id, "Failed");

        var page = await GetPageAsync(client, run.ProjectId, cursor: null);
        Assert.Equal(run.AnalysisId, page.GetProperty("analysisId").GetGuid());
    }

    [Fact]
    public async Task LatestBranchesFollowTheRequestedBaseline()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 1);
        repository.AddSynchronizedBranch(SecondaryBranch);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath,
            ApiTestWorkflow.MultiBaselineSettings("refs/heads/main", SecondaryBaseline));
        var runs = await ApiTestWorkflow.AnalyzeAllAsync(client, projectId);

        var page = await ReadLatestAsync(client, projectId, SecondaryBaseline);

        Assert.Equal(SecondaryBaseline, page.GetProperty("referenceName").GetString());
        Assert.Equal(
            runs.Single(run => run.ReferenceName == SecondaryBaseline).Id,
            page.GetProperty("analysisId").GetGuid());
    }

    [Fact]
    public async Task DeletingTheLatestCaptureFallsBackToThePreviousOne()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 1);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var run = await AnalyzeAsync(client, repository.RepositoryPath);
        var earlier = await ReadLatestAsync(client, run.ProjectId);
        var newest = await ApiTestWorkflow.AnalyzeAsync(client, run.ProjectId);
        Assert.Equal(newest, AnalysisId(await ReadLatestAsync(client, run.ProjectId)));

        await DeleteCaptureAsync(client, newest);

        var restored = await ReadLatestAsync(client, run.ProjectId);
        Assert.Equal(run.AnalysisId, AnalysisId(restored));
        Assert.Equal(CapturedAt(earlier), CapturedAt(restored));
    }

    [Fact]
    public async Task DeletingTheOnlyCaptureLeavesNoSnapshot()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 1);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var run = await AnalyzeAsync(client, repository.RepositoryPath);

        await DeleteCaptureAsync(client, run.AnalysisId);

        using var response = await client.GetAsync(
            $"/api/projects/{run.ProjectId}/analyses/latest/branches");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "analysis.no_successful_result",
            await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task DeletingAnUnknownCaptureIsNotFound()
    {
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.DeleteAsync($"/api/analyses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "analysis.not_found",
            await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    /// <summary>
    /// A merged branch adds no commit, so it has no top contributor — only its tip author
    /// still says whose branch it is, which is why the UI filters on that instead.
    /// </summary>
    [Fact]
    public async Task TopContributorNamesTheAuthorOfTheBranchCommits()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        repository.AddAheadBranchWithAuthor("feature/owned", BranchAuthor);
        repository.AddSynchronizedBranch("feature/merged");
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var run = await AnalyzeAsync(client, repository.RepositoryPath);

        var owned = await GetSingleSnapshotAsync(client, run.ProjectId, "owned");
        var merged = await GetSingleSnapshotAsync(client, run.ProjectId, "merged");

        var contributor = owned.GetProperty("topContributor");
        Assert.Equal(BranchAuthor, contributor.GetProperty("name").GetString());
        Assert.Equal(1, contributor.GetProperty("commitCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, merged.GetProperty("topContributor").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(owned.GetProperty("tipAuthor").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(merged.GetProperty("tipAuthor").GetString()));
    }

    private static ApiApplicationFactory CreateFactory(string repositoriesRoot) => new()
    {
        RepositoriesRoot = repositoriesRoot,
    };

    private static async Task DeleteCaptureAsync(HttpClient client, Guid analysisId)
    {
        using var response = await client.DeleteAsync($"/api/analyses/{analysisId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<JsonElement> ReadLatestAsync(
        HttpClient client,
        Guid projectId,
        string? baseline = null)
    {
        var uri = $"/api/projects/{projectId}/analyses/latest/branches";
        if (baseline is not null)
        {
            uri += $"?baseline={Uri.EscapeDataString(baseline)}";
        }

        return await client.GetFromJsonAsync<JsonElement>(uri);
    }

    private static Guid AnalysisId(JsonElement page) =>
        page.GetProperty("analysisId").GetGuid();

    private static DateTimeOffset CapturedAt(JsonElement page) =>
        page.GetProperty("capturedAtUtc").GetDateTimeOffset();

    private static async Task<(Guid ProjectId, Guid AnalysisId)> AnalyzeAsync(
        HttpClient client,
        string repositoryPath)
    {
        var projectId = await ApiTestWorkflow.CreateProjectAsync(client, repositoryPath);
        var launch = await ApiTestWorkflow.LaunchAsync(client, projectId);
        using var response = launch.Response;
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal($"/api/analyses/{launch.Id}", response.Headers.Location?.OriginalString);
        await ApiTestWorkflow.WaitForStatusAsync(client, launch.Id, "Completed");
        return (projectId, launch.Id);
    }

    private static async Task<JsonElement> GetPageAsync(
        HttpClient client,
        Guid projectId,
        string? cursor)
    {
        var query = $"search=near-&sort=name&direction=asc&pageSize=2";
        if (cursor is not null)
        {
            query += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return await client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{projectId}/analyses/latest/branches?{query}");
    }

    private static async Task AssertChangedFilterIsRejectedAsync(
        HttpClient client,
        Guid projectId,
        string cursor)
    {
        var uri = $"/api/projects/{projectId}/analyses/latest/branches"
            + $"?search=other&sort=name&direction=asc&pageSize=2"
            + $"&cursor={Uri.EscapeDataString(cursor)}";
        using var response = await client.GetAsync(uri);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "pagination.invalid_cursor",
            await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    private static async Task AssertDetailAsync(HttpClient client, JsonElement item)
    {
        var snapshotId = SnapshotId(item);
        var detail = await client.GetFromJsonAsync<JsonElement>(
            $"/api/branch-snapshots/{snapshotId}");
        Assert.Equal(snapshotId, detail.GetProperty("snapshot").GetProperty("id").GetGuid());
        Assert.NotEmpty(detail.GetProperty("contributors").EnumerateArray());
        Assert.True(detail.GetProperty("snapshot").GetProperty("isProtected").GetBoolean());
        Assert.Equal("Available", detail.GetProperty("attributionStatus").GetString());
        Assert.True(detail.GetProperty("mailmapApplied").GetBoolean());
        Assert.Equal(
            ["refs/heads/feature/near-*"],
            detail.GetProperty("policy").GetProperty("protectedPatterns")
                .EnumerateArray()
                .Select(pattern => pattern.GetString()));
    }

    private static async Task<JsonElement> GetSingleSnapshotAsync(
        HttpClient client,
        Guid projectId,
        string search)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{projectId}/analyses/latest/branches?search={search}");
        return Assert.Single(page.GetProperty("items").EnumerateArray());
    }

    private static async Task<JsonElement> GetAnalysisSnapshotAsync(
        HttpClient client,
        Guid analysisId,
        string search)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(
            $"/api/analyses/{analysisId}/branches?search={search}");
        return Assert.Single(page.GetProperty("items").EnumerateArray());
    }

    private static async Task AssertAttributionStatusAsync(
        HttpClient client,
        JsonElement snapshot,
        string expectedStatus)
    {
        var detail = await client.GetFromJsonAsync<JsonElement>(
            $"/api/branch-snapshots/{SnapshotId(snapshot)}");
        Assert.Empty(detail.GetProperty("contributors").EnumerateArray());
        Assert.Equal(expectedStatus, detail.GetProperty("attributionStatus").GetString());
    }

    private static async Task RemoveCurrentPoliciesAsync(HttpClient client, Guid projectId)
    {
        var request = new
        {
            referenceName = "refs/heads/main",
            branchNamespace = "refs/heads/feature/*",
            activeUntilDays = 30,
            inactiveAfterDays = 90,
            excludedPatterns = Array.Empty<string>(),
            protectedPatterns = Array.Empty<string>(),
        };
        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/settings",
            request);
        response.EnsureSuccessStatusCode();
    }

    private static Guid SnapshotId(JsonElement item) => item.GetProperty("id").GetGuid();

    private sealed class MutableClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }
}
