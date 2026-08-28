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
