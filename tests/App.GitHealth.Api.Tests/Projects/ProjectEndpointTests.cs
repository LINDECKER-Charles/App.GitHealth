using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Features.Projects;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.Projects;

public sealed class ProjectEndpointTests
{
    private static readonly string[] TwoBaselines =
        ["refs/heads/release", "refs/heads/main"];

    [Fact]
    public async Task ValidateRejectsMissingNonGitAndOutsidePaths()
    {
        using var allowed = GitTestRepository.Create(aheadBranchCount: 0);
        using var outside = GitTestRepository.Create(aheadBranchCount: 0);
        var nonGitPath = Path.Combine(allowed.RootPath, "not-a-repository");
        Directory.CreateDirectory(nonGitPath);
        using var factory = CreateFactory(allowed.RootPath);
        using var client = factory.CreateClient();

        await AssertProblemAsync(
            client,
            Path.Combine(allowed.RootPath, "missing"),
            "repository.invalid_path");
        await AssertProblemAsync(client, nonGitPath, "repository.invalid");
        await AssertProblemAsync(client, outside.RepositoryPath, "repository.path_not_allowed");
    }

    [Fact]
    public async Task ProjectInputsHaveExplicitSizeLimits()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();

        await AssertProblemAsync(
            client,
            new string('a', RepositoryValidator.MaximumPathLength + 1),
            "repository.invalid_path");
        using var response = await client.PostAsJsonAsync("/api/projects", new
        {
            displayName = new string('a', ProjectService.MaximumDisplayNameLength + 1),
            repositoryPath = repository.RepositoryPath,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "validation.invalid_request",
            await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ValidateRejectsSymbolicLinkLeavingAllowedRoot()
    {
        using var allowed = GitTestRepository.Create(aheadBranchCount: 0);
        using var outside = GitTestRepository.Create(aheadBranchCount: 0);
        var linkPath = Path.Combine(allowed.RootPath, "external-link");
        GitTestRepository.CreateDirectoryLink(linkPath, outside.RepositoryPath);

        using var factory = CreateFactory(allowed.RootPath);
        using var client = factory.CreateClient();
        try
        {
            await AssertProblemAsync(client, linkPath, "repository.path_not_allowed");
        }
        finally
        {
            Directory.Delete(linkPath);
        }
    }

    [Fact]
    public async Task ValidateAcceptsPhysicalPathBehindConfiguredRootLink()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        var linkPath = $"{repository.RootPath}-link";
        GitTestRepository.CreateDirectoryLink(linkPath, repository.RootPath);

        using var factory = CreateFactory(linkPath);
        using var client = factory.CreateClient();
        try
        {
            await AssertValidationAsync(client, repository.RepositoryPath);
        }
        finally
        {
            Directory.Delete(linkPath);
        }
    }

    [Fact]
    public async Task ProjectCanBeValidatedCreatedListedAndUpdated()
    {
        using var repository = GitTestRepository.Create();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();

        await AssertValidationAsync(client, repository.RepositoryPath);
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);
        await AssertProjectIsListedAsync(client, projectId);
        await AssertSettingsCanBeUpdatedAsync(client, projectId);
    }

    [Fact]
    public async Task ProjectCanBeCreatedWithSeveralBaselines()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 1);
        repository.AddSynchronizedBranch("release");
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();

        var project = await CreateAsync(
            client,
            repository.RepositoryPath,
            new { referenceNames = TwoBaselines });

        Assert.Equal("refs/heads/release", project.GetProperty("referenceName").GetString());
        Assert.Equal(TwoBaselines, References(project));
    }

    [Fact]
    public async Task CreatingAProjectWithoutSettingsStillPicksASingleBaseline()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 1);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();

        var project = await CreateAsync(client, repository.RepositoryPath, settings: null);

        Assert.Equal("refs/heads/main", project.GetProperty("referenceName").GetString());
        Assert.Equal(["refs/heads/main"], References(project));
    }

    private static ApiApplicationFactory CreateFactory(string repositoriesRoot) => new()
    {
        RepositoriesRoot = repositoriesRoot,
    };

    private static async Task<JsonElement> CreateAsync(
        HttpClient client,
        string repositoryPath,
        object? settings)
    {
        using var response = await client.PostAsJsonAsync("/api/projects", new
        {
            displayName = "API repository",
            repositoryPath,
            settings,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string[] References(JsonElement project) =>
        project.GetProperty("referenceNames")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();

    private static async Task AssertProblemAsync(
        HttpClient client,
        string path,
        string expectedCode)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/projects/validate",
            new { path });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    private static async Task AssertValidationAsync(HttpClient client, string path)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/projects/validate",
            new { path });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Path.GetFullPath(path), payload.GetProperty("canonicalPath").GetString());
        Assert.Contains(
            payload.GetProperty("references").EnumerateArray(),
            reference => reference.GetString() == "refs/heads/main");
    }

    private static async Task AssertProjectIsListedAsync(HttpClient client, Guid projectId)
    {
        var projects = await client.GetFromJsonAsync<JsonElement[]>("/api/projects");
        Assert.NotNull(projects);
        var project = Assert.Single(
            projects,
            candidate => candidate.GetProperty("id").GetGuid() == projectId);
        var createdAt = project.GetProperty("createdAtUtc").GetDateTimeOffset();
        var updatedAt = project.GetProperty("updatedAtUtc").GetDateTimeOffset();
        Assert.Equal(TimeSpan.Zero, createdAt.Offset);
        Assert.Equal(createdAt, updatedAt);
    }

    private static async Task AssertSettingsCanBeUpdatedAsync(HttpClient client, Guid projectId)
    {
        var request = new
        {
            referenceName = "refs/heads/main",
            branchNamespace = "refs/heads/feature/*",
            activeUntilDays = 7,
            inactiveAfterDays = 21,
            excludedPatterns = new[] { "refs/heads/feature/behind" },
            protectedPatterns = Array.Empty<string>(),
        };
        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/settings",
            request);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(7, payload.GetProperty("activeUntilDays").GetInt32());
        Assert.Equal(21, payload.GetProperty("inactiveAfterDays").GetInt32());
        Assert.True(
            payload.GetProperty("updatedAtUtc").GetDateTimeOffset()
                >= payload.GetProperty("createdAtUtc").GetDateTimeOffset());
    }

}
