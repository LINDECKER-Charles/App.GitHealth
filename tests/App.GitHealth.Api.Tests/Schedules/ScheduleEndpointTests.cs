using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.Schedules;

public sealed class ScheduleEndpointTests
{
    private const string EveryMorning = "0 9 * * *";

    [Fact]
    public async Task ANewProjectHasNoScheduleAndNoNextRun()
    {
        using var repository = GitTestRepository.Create();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        var schedule = await ReadScheduleAsync(client, projectId);

        Assert.False(schedule.GetProperty("isEnabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, schedule.GetProperty("cronExpression").ValueKind);
        Assert.Equal(JsonValueKind.Null, schedule.GetProperty("lastRunAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, schedule.GetProperty("nextRunAtUtc").ValueKind);
        Assert.Equal("UTC", schedule.GetProperty("timeZoneId").GetString());
        Assert.True(schedule.GetProperty("isSchedulerRunning").GetBoolean());
    }

    [Fact]
    public async Task SavingAScheduleNamesWhenItWillNextFire()
    {
        using var repository = GitTestRepository.Create();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        var saved = await SaveScheduleAsync(client, projectId, Draft("  0   9  *  *  * "));

        Assert.True(saved.GetProperty("isEnabled").GetBoolean());
        Assert.Equal(EveryMorning, saved.GetProperty("cronExpression").GetString());
        var next = saved.GetProperty("nextRunAtUtc").GetDateTimeOffset();
        Assert.Equal(9, next.Hour);
        Assert.Equal(0, next.Minute);
        Assert.True(next > DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Switching off keeps the expression. Coming back to a repository a fortnight later and
    /// finding the field empty would make the switch a delete button wearing a disguise.
    /// </summary>
    [Fact]
    public async Task SwitchingOffKeepsTheExpressionAndStopsPlanning()
    {
        using var repository = GitTestRepository.Create();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);
        await SaveScheduleAsync(client, projectId, Draft(EveryMorning));

        var stopped = await SaveScheduleAsync(
            client,
            projectId,
            new ScheduleDraft(false, EveryMorning));

        Assert.False(stopped.GetProperty("isEnabled").GetBoolean());
        Assert.Equal(EveryMorning, stopped.GetProperty("cronExpression").GetString());
        Assert.Equal(JsonValueKind.Null, stopped.GetProperty("nextRunAtUtc").ValueKind);
    }

    [Fact]
    public async Task ClearingTheExpressionForgetsTheSchedule()
    {
        using var repository = GitTestRepository.Create();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);
        await SaveScheduleAsync(client, projectId, Draft(EveryMorning));

        var cleared = await SaveScheduleAsync(client, projectId, new ScheduleDraft(false, null));

        Assert.False(cleared.GetProperty("isEnabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("cronExpression").ValueKind);
    }

    [Theory]
    [InlineData(true, "every tuesday")]
    [InlineData(true, "* * * *")]
    [InlineData(true, "70 9 * * *")]
    [InlineData(true, null)]
    [InlineData(false, "nonsense")]
    public async Task AScheduleThatCannotBeReadIsRefused(bool isEnabled, string? expression)
    {
        using var repository = GitTestRepository.Create();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        using var response = await PutScheduleAsync(
            client,
            projectId,
            new ScheduleDraft(isEnabled, expression));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("schedule.invalid", await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task AnUnknownProjectHasNoSchedule()
    {
        using var repository = GitTestRepository.Create();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/projects/{Guid.NewGuid()}/schedule");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("project.not_found", await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    private static ApiApplicationFactory CreateFactory(string repositoriesRoot) => new()
    {
        RepositoriesRoot = repositoriesRoot,
    };

    private static ScheduleDraft Draft(string expression) => new(true, expression);

    private static async Task<JsonElement> ReadScheduleAsync(HttpClient client, Guid projectId)
    {
        using var response = await client.GetAsync($"/api/projects/{projectId}/schedule");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> SaveScheduleAsync(
        HttpClient client,
        Guid projectId,
        ScheduleDraft draft)
    {
        using var response = await PutScheduleAsync(client, projectId, draft);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> PutScheduleAsync(
        HttpClient client,
        Guid projectId,
        ScheduleDraft draft) => client.PutAsJsonAsync(
            $"/api/projects/{projectId}/schedule",
            new { isEnabled = draft.IsEnabled, cronExpression = draft.CronExpression });

    /// <summary>What the caller writes: the switch and the expression, together.</summary>
    private sealed record ScheduleDraft(bool IsEnabled, string? CronExpression);
}
