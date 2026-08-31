using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace App.GitHealth.Api.Tests.Hosting;

internal static class ApiTestWorkflow
{
    private const int PollAttempts = 200;
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(25);

    public static async Task<Guid> CreateProjectAsync(HttpClient client, string repositoryPath)
    {
        var request = new
        {
            displayName = "API repository",
            repositoryPath,
            settings = DefaultSettings(),
        };
        using var response = await client.PostAsJsonAsync("/api/projects", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("id").GetGuid();
    }

    public static async Task<(Guid Id, bool IsDuplicate, HttpResponseMessage Response)> LaunchAsync(
        HttpClient client,
        Guid projectId)
    {
        var response = await client.PostAsync(
            $"/api/projects/{projectId}/analyses",
            content: null);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (
            payload.GetProperty("analysisId").GetGuid(),
            payload.GetProperty("isDuplicate").GetBoolean(),
            response);
    }

    public static async Task<Guid> AnalyzeAsync(HttpClient client, Guid projectId)
    {
        var launch = await LaunchAsync(client, projectId);
        using var response = launch.Response;
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await WaitForStatusAsync(client, launch.Id, "Completed");
        return launch.Id;
    }

    public static async Task<JsonElement> WaitForStatusAsync(
        HttpClient client,
        Guid analysisId,
        string expectedStatus)
    {
        for (var attempt = 0; attempt < PollAttempts; attempt++)
        {
            var payload = await GetStatusAsync(client, analysisId);
            if (payload.GetProperty("status").GetString() == expectedStatus)
            {
                return payload;
            }

            await Task.Delay(PollDelay);
        }

        throw new TimeoutException($"Analysis {analysisId} did not reach {expectedStatus}.");
    }

    public static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("traceId", out _));
        return payload.GetProperty("code").GetString();
    }

    public static object DefaultSettings() => new
    {
        referenceName = "refs/heads/main",
        branchNamespace = "refs/heads/feature/*",
        activeUntilDays = 30,
        inactiveAfterDays = 90,
        excludedPatterns = Array.Empty<string>(),
        protectedPatterns = new[] { "refs/heads/feature/near-*" },
    };

    private static async Task<JsonElement> GetStatusAsync(HttpClient client, Guid analysisId)
    {
        using var response = await client.GetAsync($"/api/analyses/{analysisId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
