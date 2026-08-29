using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;
using App.GitHealth.Core.Projects;

namespace App.GitHealth.Api.Tests.Projects;

public sealed class ProjectOrganizationEndpointTests
{
    [Fact]
    public async Task FavoriteAndGroupAreStoredThenListed()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(client, repository.RepositoryPath);

        var organized = await OrganizeAsync(client, projectId, Favorite("  Back-office  "));

        Assert.True(organized.GetProperty("isFavorite").GetBoolean());
        Assert.Equal("Back-office", organized.GetProperty("groupName").GetString());
        await AssertListedGroupAsync(client, projectId, "Back-office");
    }

    [Fact]
    public async Task AnEmptyGroupNameTakesTheProjectOutOfItsGroup()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(client, repository.RepositoryPath);
        await OrganizeAsync(client, projectId, Favorite("Back-office"));

        var organized = await OrganizeAsync(client, projectId, new Organization(false, "   "));

        Assert.False(organized.GetProperty("isFavorite").GetBoolean());
        Assert.Equal(JsonValueKind.Null, organized.GetProperty("groupName").ValueKind);
    }

    [Fact]
    public async Task AGroupNameHasAnExplicitSizeLimit()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(client, repository.RepositoryPath);
        var tooLong = new string('a', ProjectOrganization.MaximumGroupNameLength + 1);

        using var response = await PutAsync(client, projectId, new Organization(false, tooLong));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "validation.invalid_request",
            await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task AnUnknownProjectCannotBeOrganized()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();

        using var response = await PutAsync(client, Guid.NewGuid(), Favorite("Back-office"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("project.not_found", await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    private sealed record Organization(bool IsFavorite, string? GroupName);

    private static Organization Favorite(string groupName) => new(true, groupName);

    private static ApiApplicationFactory CreateFactory(string repositoriesRoot) => new()
    {
        RepositoriesRoot = repositoriesRoot,
    };

    private static async Task<JsonElement> OrganizeAsync(
        HttpClient client,
        Guid projectId,
        Organization organization)
    {
        using var response = await PutAsync(client, projectId, organization);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> PutAsync(
        HttpClient client,
        Guid projectId,
        Organization organization)
    {
        return client.PutAsJsonAsync(
            $"/api/projects/{projectId}/organization",
            new { organization.IsFavorite, organization.GroupName });
    }

    private static async Task AssertListedGroupAsync(
        HttpClient client,
        Guid projectId,
        string expectedGroup)
    {
        var projects = await client.GetFromJsonAsync<JsonElement[]>("/api/projects");
        Assert.NotNull(projects);
        var project = Assert.Single(
            projects,
            candidate => candidate.GetProperty("id").GetGuid() == projectId);
        Assert.Equal(expectedGroup, project.GetProperty("groupName").GetString());
        Assert.True(project.GetProperty("isFavorite").GetBoolean());
    }
}
