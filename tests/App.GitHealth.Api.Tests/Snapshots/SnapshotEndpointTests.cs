using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.Snapshots;

public sealed class SnapshotEndpointTests
{
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
        Assert.All(firstItems, item => Assert.True(item.GetProperty("isProtected").GetBoolean()));
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

    private static ApiApplicationFactory CreateFactory(string repositoriesRoot) => new()
    {
        RepositoriesRoot = repositoriesRoot,
    };

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
}
