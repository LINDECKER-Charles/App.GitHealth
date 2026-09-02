using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.Assistant;

public sealed class AssistantEndpointTests
{
    private const string BranchAuthor = "Ada Lovelace";
    private const string Question = "Which branches can I clean up?";

    [Fact]
    public async Task TheCatalogIsListedWhateverIsInstalledOnTheMachine()
    {
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/api/assistant/agents");

        Assert.True(payload.GetProperty("isEnabled").GetBoolean());
        var agents = payload.GetProperty("agents").EnumerateArray().ToArray();
        Assert.Equal(["claude", "codex"], agents.Select(agent => Text(agent, "id")));
        Assert.All(agents, agent => Assert.False(
            string.IsNullOrWhiteSpace(Text(agent, "installationUrl"))));
    }

    /// <summary>
    /// An agent that is not installed says where the search looked. A greyed-out button with
    /// no explanation would leave the user with nothing to act on.
    /// </summary>
    [Fact]
    public async Task AnAgentIsEitherAvailableWithAVersionOrExplainedWithoutOne()
    {
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/api/assistant/agents");

        foreach (var agent in payload.GetProperty("agents").EnumerateArray())
        {
            var isAvailable = agent.GetProperty("isAvailable").GetBoolean();
            Assert.Equal(isAvailable, Text(agent, "version") is not null);
            Assert.Equal(isAvailable, Text(agent, "unavailableReason") is null);
        }
    }

    [Fact]
    public async Task DisablingTheAssistantEmptiesTheCatalogAndRefusesARun()
    {
        using var factory = new ApiApplicationFactory { AssistantEnabled = false };
        using var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/api/assistant/agents");
        Assert.False(payload.GetProperty("isEnabled").GetBoolean());
        Assert.Empty(payload.GetProperty("agents").EnumerateArray());

        using var run = await StartAsync(client, Guid.NewGuid(), "claude", Question);
        Assert.Equal(HttpStatusCode.Forbidden, run.StatusCode);
        Assert.Equal("assistant.disabled", await CodeAsync(run));
    }

    [Fact]
    public async Task ARunWithoutAQuestionIsRefusedBeforeAnythingIsLaunched()
    {
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await StartAsync(client, Guid.NewGuid(), "claude", "   ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("assistant.question_required", await CodeAsync(response));
    }

    /// <summary>
    /// The interface names an agent; the catalog decides. Anything outside it is refused
    /// rather than launched, which is what stops this endpoint being a way to run a command.
    /// </summary>
    [Fact]
    public async Task AnAgentOutsideTheCatalogIsRefused()
    {
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await StartAsync(client, Guid.NewGuid(), "/bin/sh", Question);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("assistant.agent_unavailable", await CodeAsync(response));
    }

    [Fact]
    public async Task EachAgentPublishesTheEffortLevelsItAcceptsAndItsDefault()
    {
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/api/assistant/agents");

        foreach (var agent in payload.GetProperty("agents").EnumerateArray())
        {
            var efforts = agent.GetProperty("efforts")
                .EnumerateArray()
                .Select(effort => effort.GetString() ?? string.Empty)
                .ToArray();
            Assert.Equal(["low", "medium", "high", "xhigh", "max"], efforts);
            Assert.Contains(Text(agent, "defaultEffort"), efforts);
        }
    }

    /// <summary>
    /// The effort ends up inside a command line, so it is allowlisted rather than trusted.
    /// Refusing beats quietly falling back: a run at the wrong effort costs real money.
    /// </summary>
    [Fact]
    public async Task AnEffortTheAgentDoesNotAcceptIsRefused()
    {
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{Guid.NewGuid()}/assistant/runs",
            new
            {
                agentId = "claude",
                question = Question,
                effort = "--dangerously-skip-permissions",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("assistant.effort_unsupported", await CodeAsync(response));
    }

    [Fact]
    public async Task AnUnknownRunIsNotFound()
    {
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/assistant/runs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("assistant.run_not_found", await CodeAsync(response));
    }

    [Fact]
    public async Task ABriefingIsRefusedForAProjectThatDoesNotExist()
    {
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/projects/{Guid.NewGuid()}/assistant/briefing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("project.not_found", await CodeAsync(response));
    }

    [Fact]
    public async Task ABriefingIsRefusedUntilAnAnalysisHasSucceeded()
    {
        using var repository = GitTestRepository.Create();
        using var factory = new ApiApplicationFactory { RepositoriesRoot = repository.RootPath };
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        using var response = await client.GetAsync(
            $"/api/projects/{projectId}/assistant/briefing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("analysis.no_successful_result", await CodeAsync(response));
    }

    /// <summary>
    /// The briefing is the consent screen: what it returns is what would leave the machine,
    /// so the test reads it the way the user does.
    /// </summary>
    [Fact]
    public async Task TheBriefingCarriesTheCaptureThatWouldBeSent()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 2);
        repository.AddAheadBranchWithAuthor("feature/reporting", BranchAuthor);
        using var factory = new ApiApplicationFactory { RepositoriesRoot = repository.RootPath };
        using var client = factory.CreateClient();
        var projectId = await AnalyzeAsync(client, repository.RepositoryPath);

        var payload = await client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{projectId}/assistant/briefing");

        var text = payload.GetProperty("text").GetString()!;
        Assert.Equal("refs/heads/main", payload.GetProperty("baseline").GetString());
        Assert.Equal(0, payload.GetProperty("omittedBranchCount").GetInt32());
        Assert.True(payload.GetProperty("branchCount").GetInt32() >= 3);
        Assert.Contains("# Branch capture", text, StringComparison.Ordinal);
        Assert.Contains("refs/heads/feature/reporting", text, StringComparison.Ordinal);
        Assert.Contains(BranchAuthor, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Contributor addresses are the most identifying thing GitHealth holds, and the least
    /// useful to a reading of the branches. They stay on the machine.
    /// </summary>
    [Fact]
    public async Task TheBriefingNamesAuthorsWithoutCarryingTheirAddresses()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 1);
        repository.AddAheadBranchWithAuthor("feature/reporting", BranchAuthor);
        using var factory = new ApiApplicationFactory { RepositoriesRoot = repository.RootPath };
        using var client = factory.CreateClient();
        var projectId = await AnalyzeAsync(client, repository.RepositoryPath);

        var payload = await client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{projectId}/assistant/briefing");

        Assert.DoesNotContain("@", payload.GetProperty("text").GetString()!, StringComparison.Ordinal);
    }

    private static async Task<Guid> AnalyzeAsync(HttpClient client, string repositoryPath)
    {
        var projectId = await ApiTestWorkflow.CreateProjectAsync(client, repositoryPath);
        await ApiTestWorkflow.AnalyzeAsync(client, projectId);
        return projectId;
    }

    private static Task<HttpResponseMessage> StartAsync(
        HttpClient client,
        Guid projectId,
        string agentId,
        string question) => client.PostAsJsonAsync(
            $"/api/projects/{projectId}/assistant/runs",
            new { agentId, question });

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("code").GetString();
    }

    private static string? Text(JsonElement element, string name)
    {
        var property = element.GetProperty(name);
        return property.ValueKind == JsonValueKind.Null ? null : property.GetString();
    }
}
