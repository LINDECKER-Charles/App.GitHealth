using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.Analyses;

public sealed class AnalysisHistoryEndpointTests
{
    private const string BranchNamespace = "refs/heads/feature/*";

    [Fact]
    public async Task HistoryIsPagedAndPreservesEachAnalysisPolicy()
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
        var firstAnalysisId = await ApiTestWorkflow.AnalyzeAsync(client, projectId);
        await UpdatePolicyAsync(client, projectId);
        var secondAnalysisId = await ApiTestWorkflow.AnalyzeAsync(client, projectId);

        var analysisIds = new AnalysisHistoryIds(firstAnalysisId, secondAnalysisId);
        await AssertHistoryAsync(client, projectId, analysisIds);
        await AssertCapturedPolicyAsync(client, firstAnalysisId, DefaultPolicy());
        await AssertCapturedPolicyAsync(client, secondAnalysisId, UpdatedPolicy());
        await AssertInvalidPageAsync(client, projectId);
    }

    private static async Task UpdatePolicyAsync(HttpClient client, Guid projectId)
    {
        var request = new
        {
            activeUntilDays = 7,
            inactiveAfterDays = 21,
            excludedPatterns = new[] { "refs/heads/feature/near-01" },
            protectedPatterns = new[] { "refs/heads/feature/near-00" },
        };
        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/policy",
            request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task AssertHistoryAsync(
        HttpClient client,
        Guid projectId,
        AnalysisHistoryIds analysisIds)
    {
        var latest = await ReadHistoryItemAsync(client, projectId, page: 1);
        AssertHistoryItem(latest, analysisIds.Second, UpdatedPolicy());

        var oldest = await ReadHistoryItemAsync(client, projectId, page: 2);
        AssertHistoryItem(oldest, analysisIds.First, DefaultPolicy());
    }

    private static async Task<JsonElement> ReadHistoryItemAsync(
        HttpClient client,
        Guid projectId,
        int page)
    {
        var payload = await client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{projectId}/analyses?page={page}&pageSize=1");
        Assert.Equal(page, payload.GetProperty("page").GetInt32());
        Assert.Equal(1, payload.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, payload.GetProperty("totalCount").GetInt32());
        return Assert.Single(payload.GetProperty("items").EnumerateArray().ToArray());
    }

    private static void AssertHistoryItem(
        JsonElement item,
        Guid expectedAnalysisId,
        PolicyExpectation expectedPolicy)
    {
        Assert.Equal(expectedAnalysisId, item.GetProperty("analysisId").GetGuid());
        Assert.Equal("Completed", item.GetProperty("status").GetString());
        Assert.Equal(BranchNamespace, item.GetProperty("branchNamespace").GetString());
        Assert.Equal(
            expectedPolicy.ActiveUntilDays,
            item.GetProperty("activeUntilDays").GetInt32());
        Assert.Equal(
            expectedPolicy.InactiveAfterDays,
            item.GetProperty("inactiveAfterDays").GetInt32());
        AssertPatterns(item, "excludedPatterns", expectedPolicy.ExcludedPatterns);
        AssertPatterns(item, "protectedPatterns", expectedPolicy.ProtectedPatterns);
    }

    private static async Task AssertCapturedPolicyAsync(
        HttpClient client,
        Guid analysisId,
        PolicyExpectation expected)
    {
        var payload = await client.GetFromJsonAsync<JsonElement>(
            $"/api/analyses/{analysisId}/branches?pageSize=200");
        Assert.Equal(analysisId, payload.GetProperty("analysisId").GetGuid());
        AssertPolicy(payload.GetProperty("policy"), expected);
    }

    private static void AssertPolicy(JsonElement policy, PolicyExpectation expected)
    {
        Assert.Equal(
            expected.ActiveUntilDays,
            policy.GetProperty("activeUntilDays").GetInt32());
        Assert.Equal(
            expected.InactiveAfterDays,
            policy.GetProperty("inactiveAfterDays").GetInt32());
        AssertPatterns(policy, "excludedPatterns", expected.ExcludedPatterns);
        AssertPatterns(policy, "protectedPatterns", expected.ProtectedPatterns);
    }

    private static void AssertPatterns(
        JsonElement owner,
        string propertyName,
        IReadOnlyList<string> expected)
    {
        var actual = owner.GetProperty(propertyName)
            .EnumerateArray()
            .Select(pattern => pattern.GetString()!)
            .ToArray();
        Assert.Equal(expected, actual);
    }

    private static async Task AssertInvalidPageAsync(HttpClient client, Guid projectId)
    {
        using var response = await client.GetAsync(
            $"/api/projects/{projectId}/analyses?page=0&pageSize=20");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "pagination.invalid_page",
            await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    private static PolicyExpectation DefaultPolicy() => new()
    {
        ActiveUntilDays = 30,
        InactiveAfterDays = 90,
        ProtectedPatterns = ["refs/heads/feature/near-*"],
    };

    private static PolicyExpectation UpdatedPolicy() => new()
    {
        ActiveUntilDays = 7,
        InactiveAfterDays = 21,
        ExcludedPatterns = ["refs/heads/feature/near-01"],
        ProtectedPatterns = ["refs/heads/feature/near-00"],
    };

    private sealed record PolicyExpectation
    {
        public required int ActiveUntilDays { get; init; }

        public required int InactiveAfterDays { get; init; }

        public IReadOnlyList<string> ExcludedPatterns { get; init; } = [];

        public IReadOnlyList<string> ProtectedPatterns { get; init; } = [];
    }

    private sealed record AnalysisHistoryIds(Guid First, Guid Second);
}
