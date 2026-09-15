using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.LocalApi;

/// <summary>
/// The surface an orchestrator actually reaches: real Kestrel, a real socket, and a bearer
/// token as the only thing standing in front of it.
/// </summary>
public sealed class LocalApiListenerTests
{
    private const string ProjectsPath = "/v1/projects";

    [Fact]
    public async Task RequestWithoutATokenIsRefusedAndChallenged()
    {
        using var factory = new ApiApplicationFactory();
        using var control = factory.CreateClient();
        using var access = await LocalApiWorkspace.OpenAsync(control);
        using var anonymous = LocalApiWorkspace.CreateClient(access.Port, token: null);

        using var response = await anonymous.GetAsync(ProjectsPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
        Assert.Equal("local_api.token_rejected", await ReadCodeAsync(response));
    }

    [Fact]
    public async Task RequestWithTheWrongTokenIsRefused()
    {
        using var factory = new ApiApplicationFactory();
        using var control = factory.CreateClient();
        using var access = await LocalApiWorkspace.OpenAsync(control);
        using var impostor = LocalApiWorkspace.CreateClient(access.Port, "not-the-token");

        using var response = await impostor.GetAsync(ProjectsPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A name that resolves to 127.0.0.1 is how a web page would try to reach a listener it
    /// has no business reaching, so the host is checked before the token is even weighed.
    /// </summary>
    [Fact]
    public async Task RequestWithANonLoopbackHostIsRefused()
    {
        using var factory = new ApiApplicationFactory();
        using var control = factory.CreateClient();
        using var access = await LocalApiWorkspace.OpenAsync(control);
        using var request = new HttpRequestMessage(HttpMethod.Get, ProjectsPath);
        request.Headers.Host = "githealth.example";

        using var response = await access.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("security.invalid_host", await ReadCodeAsync(response));
    }

    [Fact]
    public async Task IssuingANewTokenRevokesThePreviousOne()
    {
        using var factory = new ApiApplicationFactory();
        using var control = factory.CreateClient();
        using var access = await LocalApiWorkspace.OpenAsync(control);

        var replacement = await LocalApiWorkspace.IssueTokenAsync(control);
        using var revoked = await access.Client.GetAsync(ProjectsPath);
        using var current = LocalApiWorkspace.CreateClient(access.Port, replacement);
        using var accepted = await current.GetAsync(ProjectsPath);

        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task IndexNamesEveryRouteTheSurfaceAnswers()
    {
        using var factory = new ApiApplicationFactory();
        using var control = factory.CreateClient();
        using var access = await LocalApiWorkspace.OpenAsync(control);

        using var response = await access.Client.GetAsync("/v1");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var routes = payload.GetProperty("routes")
            .EnumerateArray()
            .Select(route => route.GetString())
            .ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("v1", payload.GetProperty("surface").GetString());
        Assert.Contains("GET /v1/projects", routes);
        Assert.Contains("POST /v1/projects/{projectId}/analyses", routes);
    }

    [Fact]
    public async Task ReadsTheProjectsAndTheBranchesOfTheirLastCapture()
    {
        using var repository = GitTestRepository.Create();
        using var factory = new ApiApplicationFactory { RepositoriesRoot = repository.RootPath };
        using var control = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(control, repository.RepositoryPath);
        await ApiTestWorkflow.AnalyzeAsync(control, projectId);
        using var access = await LocalApiWorkspace.OpenAsync(control);

        var projects = await ReadAsync(access.Client, ProjectsPath);
        var project = await ReadAsync(access.Client, $"{ProjectsPath}/{projectId}");
        var branches = await ReadAsync(access.Client, $"{ProjectsPath}/{projectId}/branches");

        Assert.Equal(projectId, projects.EnumerateArray().Single().GetProperty("id").GetGuid());
        Assert.Equal(projectId, project.GetProperty("id").GetGuid());
        Assert.NotEmpty(branches.GetProperty("items").EnumerateArray());
    }

    /// <summary>
    /// The action the whole feature exists for: an orchestrator asks for a scan, and reads it
    /// through to its capture without ever touching the interface.
    /// </summary>
    [Fact]
    public async Task LaunchesAnAnalysisAndFollowsItToItsCapture()
    {
        using var repository = GitTestRepository.Create();
        using var factory = new ApiApplicationFactory { RepositoriesRoot = repository.RootPath };
        using var control = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(control, repository.RepositoryPath);
        using var access = await LocalApiWorkspace.OpenAsync(control);

        using var launch = await access.Client.PostAsync(
            $"{ProjectsPath}/{projectId}/analyses",
            content: null);
        var accepted = await launch.Content.ReadFromJsonAsync<JsonElement>();
        var analysisId = accepted.GetProperty("analysisId").GetGuid();
        var completed = await WaitForCaptureAsync(access.Client, analysisId);
        var branches = await ReadAsync(access.Client, $"/v1/analyses/{analysisId}/branches");
        var history = await ReadAsync(access.Client, $"{ProjectsPath}/{projectId}/analyses");

        Assert.Equal(HttpStatusCode.Accepted, launch.StatusCode);

        // The status URL must point at this surface: /api is unreachable from here.
        Assert.Equal($"/v1/analyses/{analysisId}", accepted.GetProperty("statusUrl").GetString());
        Assert.Equal(projectId, completed.GetProperty("projectId").GetGuid());
        Assert.NotEmpty(branches.GetProperty("items").EnumerateArray());
        Assert.Equal(1, history.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ClosingTheAccessStopsAnsweringAltogether()
    {
        using var factory = new ApiApplicationFactory();
        using var control = factory.CreateClient();
        using var access = await LocalApiWorkspace.OpenAsync(control);

        var state = await LocalApiWorkspace.ApplyAsync(control, isEnabled: false, access.Port);

        // A fresh client, so the refusal is a new connection being turned away rather than a
        // pooled one being found dead.
        using var probe = LocalApiWorkspace.CreateClient(access.Port, access.Token);
        var refusal = await Record.ExceptionAsync(() => probe.GetAsync(ProjectsPath));

        Assert.Equal("Closed", state.GetProperty("status").GetString());

        // What matters is that nothing answers. How a closed port surfaces is the socket
        // stack's business, and it is not the same exception on every platform.
        Assert.True(
            refusal is HttpRequestException or SocketException,
            refusal?.ToString() ?? "The closed port still answered.");
    }

    private static async Task<JsonElement> WaitForCaptureAsync(HttpClient client, Guid analysisId)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var status = await ReadAsync(client, $"/v1/analyses/{analysisId}");
            if (status.GetProperty("status").GetString() == "Completed")
            {
                return status;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        throw new TimeoutException($"Analysis {analysisId} never completed.");
    }

    private static async Task<JsonElement> ReadAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        return problem.GetProperty("code").GetString();
    }
}
