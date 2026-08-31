using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.Policies;

public sealed class PolicyEndpointTests
{
    [Fact]
    public async Task CurrentPolicyReclassifiesLatestWithoutChangingCapturedHistory()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 2);
        using var factory = new ApiApplicationFactory
        {
            RepositoriesRoot = repository.RootPath,
        };
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);
        var analysisId = await ApiTestWorkflow.AnalyzeAsync(client, projectId);
        repository.DeleteRepository();

        var policy = ChangedPolicy();
        using var update = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/policy/",
            policy);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updatedProject = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            "refs/heads/main",
            updatedProject.GetProperty("referenceName").GetString());

        await AssertPreviewAsync(client, projectId, policy);
        var latest = await GetBranchesAsync(
            client,
            $"/api/projects/{projectId}/analyses/latest/branches");
        var historical = await GetBranchesAsync(
            client,
            $"/api/analyses/{analysisId}/branches");
        AssertPolicyContexts(latest, historical);
        AssertReclassificationPreservesFacts(latest, historical);
    }

    private static object ChangedPolicy() => new
    {
        activeUntilDays = 1,
        inactiveAfterDays = 2,
        excludedPatterns = new[] { "refs/heads/feature/near-*" },
        protectedPatterns = new[] { "refs/heads/feature/near-00" },
    };

    private static async Task AssertPreviewAsync(
        HttpClient client,
        Guid projectId,
        object policy)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/policy/preview",
            policy);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var matches = payload.GetProperty("matches").EnumerateArray().ToArray();
        Assert.Equal(
            matches.OrderBy(MatchName, StringComparer.Ordinal).Select(MatchName),
            matches.Select(MatchName));

        var protectedMatch = Find(matches, "refs/heads/feature/near-00");
        Assert.True(protectedMatch.GetProperty("isExcluded").GetBoolean());
        Assert.True(protectedMatch.GetProperty("isProtected").GetBoolean());
        Assert.Equal(
            "Protected by pattern \"refs/heads/feature/near-00\"",
            protectedMatch.GetProperty("reason").GetString());

        var excludedMatch = Find(matches, "refs/heads/feature/near-01");
        Assert.True(excludedMatch.GetProperty("isExcluded").GetBoolean());
        Assert.False(excludedMatch.GetProperty("isProtected").GetBoolean());
        Assert.Equal(
            "Excluded by pattern \"refs/heads/feature/near-*\"",
            excludedMatch.GetProperty("reason").GetString());
    }

    private static async Task<JsonElement> GetBranchesAsync(HttpClient client, string endpoint) =>
        await client.GetFromJsonAsync<JsonElement>(
            $"{endpoint}?search=near-&sort=name&direction=asc&pageSize=10");

    private static void AssertPolicyContexts(JsonElement latest, JsonElement historical)
    {
        var latestPolicy = latest.GetProperty("policy");
        Assert.Equal(1, latestPolicy.GetProperty("activeUntilDays").GetInt32());
        Assert.Equal(
            ["refs/heads/feature/near-*"],
            Strings(latestPolicy, "excludedPatterns"));
        Assert.Equal(
            ["refs/heads/feature/near-00"],
            Strings(latestPolicy, "protectedPatterns"));

        var capturedPolicy = historical.GetProperty("policy");
        Assert.Equal(30, capturedPolicy.GetProperty("activeUntilDays").GetInt32());
        Assert.Empty(Strings(capturedPolicy, "excludedPatterns"));
        Assert.Equal(
            ["refs/heads/feature/near-*"],
            Strings(capturedPolicy, "protectedPatterns"));
    }

    private static void AssertReclassificationPreservesFacts(
        JsonElement latest,
        JsonElement historical)
    {
        var latestBranch = Find(Items(latest), "refs/heads/feature/near-01");
        var historicalBranch = Find(Items(historical), "refs/heads/feature/near-01");
        Assert.True(latestBranch.GetProperty("isExcluded").GetBoolean());
        Assert.False(latestBranch.GetProperty("isProtected").GetBoolean());
        Assert.False(historicalBranch.GetProperty("isExcluded").GetBoolean());
        Assert.True(historicalBranch.GetProperty("isProtected").GetBoolean());
        Assert.Equal(
            historicalBranch.GetProperty("commitId").GetString(),
            latestBranch.GetProperty("commitId").GetString());
        Assert.Equal(
            historicalBranch.GetProperty("aheadCount").GetInt32(),
            latestBranch.GetProperty("aheadCount").GetInt32());
        Assert.Equal(
            historicalBranch.GetProperty("behindCount").GetInt32(),
            latestBranch.GetProperty("behindCount").GetInt32());
    }

    private static JsonElement[] Items(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().ToArray();

    private static JsonElement Find(IEnumerable<JsonElement> items, string referenceName) =>
        Assert.Single(items, item => MatchName(item) == referenceName);

    private static string? MatchName(JsonElement item) =>
        item.GetProperty("referenceName").GetString();

    private static string[] Strings(JsonElement owner, string propertyName) =>
        owner.GetProperty(propertyName)
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
}
