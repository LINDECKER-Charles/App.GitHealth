using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.Projects;

public sealed class BaselineEndpointTests
{
    private const string Primary = "refs/heads/main";
    private const string Secondary = "refs/heads/release";
    private const int FeatureBranchCount = 4;
    private static readonly string[] ExcludedPatterns = ["refs/heads/feature/behind"];
    private static readonly string[] ProtectedPatterns = ["refs/heads/feature/near-00"];

    [Fact]
    public async Task BaselinesAreListedInOrderWithTheirLatestCapture()
    {
        using var repository = CreateRepository();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await CreateProjectAsync(client, repository, Primary, Secondary);
        var runs = await ApiTestWorkflow.AnalyzeAllAsync(client, projectId);

        var items = await ListBaselinesAsync(client, projectId);

        Assert.Equal([Primary, Secondary], items.Select(Name));
        Assert.Equal([0, 1], items.Select(item => item.GetProperty("position").GetInt32()));
        Assert.Equal([true, false], items.Select(item => item.GetProperty("isPrimary")
            .GetBoolean()));
        foreach (var item in items)
        {
            AssertCapture(item, runs.Single(run => run.ReferenceName == Name(item)).Id);
        }
    }

    [Fact]
    public async Task AvailableReferencesListTheRepositoryBranches()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 1);
        repository.AddSynchronizedBranch("release");
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        var payload = await ReadBaselinesAsync(client, projectId);

        Assert.Equal(
            ["refs/heads/feature/behind", "refs/heads/feature/near-00", Primary, Secondary],
            Strings(payload, "availableReferences"));
    }

    [Fact]
    public async Task ReplacingTheListChangesThePrimaryBaseline()
    {
        using var repository = CreateRepository();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await CreateProjectAsync(client, repository, Primary, Secondary);

        var project = await ReplaceAsync(client, projectId, [Secondary, Primary]);

        Assert.Equal(Secondary, project.GetProperty("referenceName").GetString());
        Assert.Equal([Secondary, Primary], Strings(project, "referenceNames"));
    }

    /// <summary>
    /// The baseline list is written whole, so the endpoint must carry the rest of the
    /// settings over rather than rebuild them from nothing.
    /// </summary>
    [Fact]
    public async Task ReplacingTheBaselinesKeepsTheThresholdsAndPatterns()
    {
        using var repository = CreateRepository();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await CreateProjectAsync(client, repository, Primary, Secondary);
        await SaveDistinctivePolicyAsync(client, projectId);

        var project = await ReplaceAsync(client, projectId, [Secondary]);

        Assert.Equal([Secondary], Strings(project, "referenceNames"));
        AssertDistinctivePolicy(project);
    }

    [Fact]
    public async Task SavingThePolicyKeepsTheBaselines()
    {
        using var repository = CreateRepository();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await CreateProjectAsync(client, repository, Primary, Secondary);

        var project = await SaveDistinctivePolicyAsync(client, projectId);

        Assert.Equal(Primary, project.GetProperty("referenceName").GetString());
        Assert.Equal([Primary, Secondary], Strings(project, "referenceNames"));
        AssertDistinctivePolicy(project);
    }

    private static GitTestRepository CreateRepository()
    {
        var repository = GitTestRepository.Create();
        repository.AddSynchronizedBranch("release");
        return repository;
    }

    private static ApiApplicationFactory CreateFactory(string repositoriesRoot) => new()
    {
        RepositoriesRoot = repositoriesRoot,
    };

    private static Task<Guid> CreateProjectAsync(
        HttpClient client,
        GitTestRepository repository,
        params string[] referenceNames) =>
        ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath,
            ApiTestWorkflow.MultiBaselineSettings(referenceNames));

    private static async Task<JsonElement> ReplaceAsync(
        HttpClient client,
        Guid projectId,
        string[] referenceNames)
    {
        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/baselines/",
            new { referenceNames });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> SaveDistinctivePolicyAsync(
        HttpClient client,
        Guid projectId)
    {
        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/policy/",
            new
            {
                activeUntilDays = 3,
                inactiveAfterDays = 11,
                excludedPatterns = ExcludedPatterns,
                protectedPatterns = ProtectedPatterns,
            });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static void AssertDistinctivePolicy(JsonElement project)
    {
        Assert.Equal(3, project.GetProperty("activeUntilDays").GetInt32());
        Assert.Equal(11, project.GetProperty("inactiveAfterDays").GetInt32());
        Assert.Equal(ExcludedPatterns, Strings(project, "excludedPatterns"));
        Assert.Equal(ProtectedPatterns, Strings(project, "protectedPatterns"));
    }

    private static void AssertCapture(JsonElement baseline, Guid expectedAnalysisId)
    {
        Assert.Equal(
            expectedAnalysisId,
            baseline.GetProperty("lastSuccessfulAnalysisId").GetGuid());
        Assert.Equal(TimeSpan.Zero, baseline.GetProperty("lastCapturedAtUtc")
            .GetDateTimeOffset().Offset);
        Assert.Equal(FeatureBranchCount, baseline.GetProperty("branchCount").GetInt32());
    }

    private static async Task<JsonElement[]> ListBaselinesAsync(
        HttpClient client,
        Guid projectId)
    {
        var payload = await ReadBaselinesAsync(client, projectId);
        return payload.GetProperty("items").EnumerateArray().ToArray();
    }

    private static async Task<JsonElement> ReadBaselinesAsync(
        HttpClient client,
        Guid projectId) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/projects/{projectId}/baselines/");

    private static string? Name(JsonElement baseline) =>
        baseline.GetProperty("referenceName").GetString();

    private static string[] Strings(JsonElement owner, string propertyName) =>
        owner.GetProperty(propertyName)
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
}
