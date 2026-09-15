using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Features.Schedules;
using App.GitHealth.Api.Tests.Hosting;
using App.GitHealth.Core.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.GitHealth.Api.Tests.Schedules;

/// <summary>
/// The scheduler, driven by hand. The worker's own timer is parked for the whole of these
/// tests — the factory ticks once an hour — so every pass here is one this file asked for,
/// and what is asserted is the rule and not the timing.
/// </summary>
public sealed class ScheduledScanTests
{
    private const string EveryMinute = "* * * * *";

    private static readonly DateTimeOffset Noon =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ADueScheduleLaunchesAnAnalysisOfEveryBaseline()
    {
        using var repository = GitTestRepository.Create();
        var clock = new MutableClock(Noon);
        using var factory = CreateFactory(repository.RootPath, clock);
        using var client = factory.CreateClient();
        var projectId = await CreateScheduledProjectAsync(client, repository.RepositoryPath);

        clock.UtcNow = Noon.AddMinutes(2);
        var launched = await RunDueAsync(factory);

        Assert.Equal(1, launched);
        var run = await WaitForFirstRunAsync(client, projectId);
        Assert.Equal("refs/heads/main", run.GetProperty("referenceName").GetString());
        var schedule = await ReadScheduleAsync(client, projectId);
        Assert.Equal(
            clock.UtcNow,
            schedule.GetProperty("lastRunAtUtc").GetDateTimeOffset());
    }

    /// <summary>
    /// Saving is not a firing. The next occurrence is counted from the edit, so a reader who
    /// writes "every minute" at 12:00:30 waits for 12:01 like everyone else.
    /// </summary>
    [Fact]
    public async Task SavingAScheduleDoesNotFireItOnTheSpot()
    {
        using var repository = GitTestRepository.Create();
        var clock = new MutableClock(Noon);
        using var factory = CreateFactory(repository.RootPath, clock);
        using var client = factory.CreateClient();
        await CreateScheduledProjectAsync(client, repository.RepositoryPath);

        Assert.Equal(0, await RunDueAsync(factory));
    }

    [Fact]
    public async Task ASwitchedOffScheduleNeverFires()
    {
        using var repository = GitTestRepository.Create();
        var clock = new MutableClock(Noon);
        using var factory = CreateFactory(repository.RootPath, clock);
        using var client = factory.CreateClient();
        var projectId = await CreateScheduledProjectAsync(client, repository.RepositoryPath);
        await SaveScheduleAsync(client, projectId, isEnabled: false);

        clock.UtcNow = Noon.AddDays(3);

        Assert.Equal(0, await RunDueAsync(factory));
    }

    /// <summary>
    /// A window that opened while nothing was watching fires once, not once per window gone
    /// by. Three days of "every minute" is over four thousand missed firings; the repository
    /// is measured a single time, and the count restarts from there.
    /// </summary>
    [Fact]
    public async Task WindowsMissedWhileClosedFireOnceBetweenThem()
    {
        using var repository = GitTestRepository.Create();
        var clock = new MutableClock(Noon);
        using var factory = CreateFactory(repository.RootPath, clock);
        using var client = factory.CreateClient();
        var projectId = await CreateScheduledProjectAsync(client, repository.RepositoryPath);

        clock.UtcNow = Noon.AddDays(3);
        Assert.Equal(1, await RunDueAsync(factory));
        await WaitForFirstRunAsync(client, projectId);

        Assert.Equal(0, await RunDueAsync(factory));
    }

    private static ApiApplicationFactory CreateFactory(string repositoriesRoot, IClock clock) =>
        new()
        {
            RepositoriesRoot = repositoriesRoot,
            TestServices = services =>
            {
                services.RemoveAll<IClock>();
                services.AddSingleton(clock);
            },
        };

    private static Task<int> RunDueAsync(ApiApplicationFactory factory) =>
        factory.Services.GetRequiredService<ScheduledScanRunner>().RunDueAsync(default);

    private static async Task<Guid> CreateScheduledProjectAsync(
        HttpClient client,
        string repositoryPath)
    {
        var projectId = await ApiTestWorkflow.CreateProjectAsync(client, repositoryPath);
        await SaveScheduleAsync(client, projectId, isEnabled: true);
        return projectId;
    }

    private static async Task SaveScheduleAsync(
        HttpClient client,
        Guid projectId,
        bool isEnabled)
    {
        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/schedule",
            new { isEnabled, cronExpression = EveryMinute });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> ReadScheduleAsync(HttpClient client, Guid projectId)
    {
        using var response = await client.GetAsync($"/api/projects/{projectId}/schedule");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// The launch is asynchronous: the runner queues the work and the worker reads it. This
    /// waits for the history to hold one finished run, which is the observable effect.
    /// </summary>
    private static async Task<JsonElement> WaitForFirstRunAsync(HttpClient client, Guid projectId)
    {
        const int attempts = 200;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var history = await client.GetFromJsonAsync<JsonElement>(
                $"/api/projects/{projectId}/analyses");
            var items = history.GetProperty("items").EnumerateArray().ToArray();
            if (items.Length > 0 && items[0].GetProperty("status").GetString() == "Completed")
            {
                return items[0];
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        throw new TimeoutException("The scheduled analysis never finished.");
    }

    private sealed class MutableClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }
}
