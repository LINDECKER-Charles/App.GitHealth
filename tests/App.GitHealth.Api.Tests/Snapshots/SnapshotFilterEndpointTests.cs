using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.Snapshots;

public sealed class SnapshotFilterEndpointTests
{
    private const string CombinedFilters =
        "topology=Ahead&activity=Active&recommendation=Excluded"
        + "&isProtected=true&isExcluded=true&sort=name&direction=asc&pageSize=1";

    private const string PrimaryBaseline = "refs/heads/main";
    private const string SecondaryBaseline = "refs/heads/release";
    private const string SecondaryBranch = "release";

    [Fact]
    public async Task CombinedFiltersRemainBoundToTheCursor()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 3);
        using var factory = new ApiApplicationFactory
        {
            RepositoriesRoot = repository.RootPath,
        };
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);
        await ApiTestWorkflow.AnalyzeAsync(client, projectId);
        await ApplyMatchingPolicyAsync(client, projectId);

        var first = await GetPageAsync(client, projectId, CombinedFilters);
        var firstItem = Assert.Single(first.GetProperty("items").EnumerateArray());
        AssertCombinedClassification(firstItem);
        var cursor = first.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));

        var query = $"{CombinedFilters}&cursor={Uri.EscapeDataString(cursor!)}";
        var second = await GetPageAsync(client, projectId, query);
        var secondItem = Assert.Single(second.GetProperty("items").EnumerateArray());
        AssertCombinedClassification(secondItem);
        Assert.NotEqual(
            firstItem.GetProperty("id").GetGuid(),
            secondItem.GetProperty("id").GetGuid());

        await AssertChangedFilterIsRejectedAsync(client, projectId, cursor!);
    }

    /// <summary>
    /// The baseline is deliberately left out of the cursor payload: it picks the analysis to
    /// read, and the analysis identifier already embedded in the cursor guards the change.
    /// </summary>
    [Fact]
    public async Task ChangingTheBaselineInvalidatesTheCursor()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 3);
        repository.AddSynchronizedBranch(SecondaryBranch);
        using var factory = new ApiApplicationFactory
        {
            RepositoriesRoot = repository.RootPath,
        };
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath,
            ApiTestWorkflow.MultiBaselineSettings(PrimaryBaseline, SecondaryBaseline));
        await ApiTestWorkflow.AnalyzeAllAsync(client, projectId);

        var first = await GetPageAsync(client, projectId, BaselineQuery(PrimaryBaseline));
        var cursor = first.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));

        await AssertChangedBaselineIsRejectedAsync(client, projectId, cursor!);
    }

    private static string BaselineQuery(string baseline) =>
        $"sort=name&direction=asc&pageSize=1&baseline={Uri.EscapeDataString(baseline)}";

    private static async Task AssertChangedBaselineIsRejectedAsync(
        HttpClient client,
        Guid projectId,
        string cursor)
    {
        var uri = $"/api/projects/{projectId}/analyses/latest/branches"
            + $"?{BaselineQuery(SecondaryBaseline)}"
            + $"&cursor={Uri.EscapeDataString(cursor)}";
        using var response = await client.GetAsync(uri);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "pagination.invalid_cursor",
            await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    private static async Task ApplyMatchingPolicyAsync(HttpClient client, Guid projectId)
    {
        var policy = new
        {
            activeUntilDays = 30,
            inactiveAfterDays = 90,
            excludedPatterns = new[] { "refs/heads/feature/near-*" },
            protectedPatterns = new[] { "refs/heads/feature/near-*" },
        };
        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/policy/",
            policy);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> GetPageAsync(
        HttpClient client,
        Guid projectId,
        string query) => await client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{projectId}/analyses/latest/branches?{query}");

    private static void AssertCombinedClassification(JsonElement item)
    {
        Assert.Equal("Ahead", item.GetProperty("topology").GetString());
        Assert.Equal("Active", item.GetProperty("activity").GetString());
        Assert.Equal("Excluded", item.GetProperty("recommendation").GetString());
        Assert.True(item.GetProperty("isProtected").GetBoolean());
        Assert.True(item.GetProperty("isExcluded").GetBoolean());
    }

    private static async Task AssertChangedFilterIsRejectedAsync(
        HttpClient client,
        Guid projectId,
        string cursor)
    {
        var changed = CombinedFilters.Replace("topology=Ahead", "topology=Merged");
        var uri = $"/api/projects/{projectId}/analyses/latest/branches"
            + $"?{changed}&cursor={Uri.EscapeDataString(cursor)}";
        using var response = await client.GetAsync(uri);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "pagination.invalid_cursor",
            await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }
}
