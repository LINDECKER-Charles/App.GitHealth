using App.GitHealth.Api.Features.Analyses;
using App.GitHealth.Api.Features.Analyses.Lifecycle;
using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Repositories;

namespace App.GitHealth.Api.Features.Schedules;

/// <summary>
/// One pass over the schedules: whatever is due is launched exactly as the button launches it,
/// through <see cref="AnalysisLaunchService"/>. Going through the same door is what guarantees
/// a scheduled scan measures every baseline, honours the queue and refuses a busy project on
/// the same terms as a scan someone asked for by hand.
/// </summary>
internal sealed partial class ScheduledScanRunner(
    IServiceScopeFactory scopeFactory,
    ScheduleContext context,
    ILogger<ScheduledScanRunner> logger)
{
    /// <summary>Launches every repository whose window has opened, and says how many.</summary>
    public async Task<int> RunDueAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var now = context.UtcNow;
        var scheduled = await projects.ListScheduledAsync(cancellationToken);
        var due = scheduled.Where(project => IsDue(project, now)).ToArray();
        foreach (var project in due)
        {
            await LaunchAsync(scope.ServiceProvider, project, now);
        }

        return due.Length;
    }

    private bool IsDue(ProjectEntity project, DateTimeOffset now) =>
        context.NextRunOf(project) <= now;

    /// <summary>
    /// The firing is recorded before the launch, never after. A launch that fails would
    /// otherwise leave the window open, and the next tick would try it again a few seconds
    /// later, and the one after that: a repository that cannot be read must go quiet, not loud.
    /// </summary>
    private async Task LaunchAsync(
        IServiceProvider services,
        ProjectEntity project,
        DateTimeOffset now)
    {
        var projects = services.GetRequiredService<IProjectRepository>();
        await projects.MarkScheduleRunAsync(project.Id, now, CancellationToken.None);

        var launcher = services.GetRequiredService<AnalysisLaunchService>();
        var launch = await launcher.LaunchAsync(
            project.Id,
            new AnalysisLaunchQueryParameters(),
            CancellationToken.None);
        if (launch.IsSuccess)
        {
            LogLaunched(logger, project.Id, launch.Value!.Analyses.Count);
            return;
        }

        LogRefused(logger, project.Id, launch.Failure!.Code);
    }

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "Scheduled scan of project {ProjectId} launched {RunCount} run(s).")]
    private static partial void LogLaunched(ILogger logger, Guid projectId, int runCount);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Warning,
        Message = "Scheduled scan of project {ProjectId} was refused: {Code}.")]
    private static partial void LogRefused(ILogger logger, Guid projectId, string code);
}
