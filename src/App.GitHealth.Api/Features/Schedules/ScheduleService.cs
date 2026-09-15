using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Core.Scheduling;

namespace App.GitHealth.Api.Features.Schedules;

/// <summary>
/// Reads and writes the schedule of one repository. Writing validates the expression here and
/// nowhere else: a schedule the scheduler cannot read must be refused at the door, not
/// discovered as silence a week later.
/// </summary>
internal sealed class ScheduleService(IProjectRepository projects, ScheduleContext context)
{
    public async Task<ApiOutcome<ScheduleResponse>> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        return project is null
            ? ApiOutcome<ScheduleResponse>.Failed(ProjectNotFound())
            : ApiOutcome<ScheduleResponse>.Success(Map(project));
    }

    public async Task<ApiOutcome<ScheduleResponse>> UpdateAsync(
        Guid projectId,
        ScheduleUpdateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var schedule = Build(request);
        if (!schedule.IsSuccess)
        {
            return ApiOutcome<ScheduleResponse>.Failed(schedule.Failure!);
        }

        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ApiOutcome<ScheduleResponse>.Failed(ProjectNotFound());
        }

        var update = new ProjectScheduleUpdate(projectId, schedule.Value!, context.UtcNow);
        await projects.UpdateScheduleAsync(update, cancellationToken);
        var updated = await projects.GetAsync(projectId, cancellationToken);
        return ApiOutcome<ScheduleResponse>.Success(Map(updated!));
    }

    private ScheduleResponse Map(ProjectEntity project)
    {
        var schedule = project.ReadSchedule();
        return new ScheduleResponse
        {
            IsEnabled = schedule.IsEnabled,
            CronExpression = schedule.Expression?.Text,
            LastRunAtUtc = project.ScheduleLastRunAtUtc,
            NextRunAtUtc = context.NextRunOf(project),
            TimeZoneId = context.Zone.Id,
            IsSchedulerRunning = context.IsRunning,
        };
    }

    /// <summary>
    /// An expression is kept whenever one is written, switched on or not. Switching on without
    /// one is refused rather than stored: it would read as armed and never fire.
    /// </summary>
    private static ApiOutcome<ScanSchedule> Build(ScheduleUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CronExpression))
        {
            return request.IsEnabled
                ? ApiOutcome<ScanSchedule>.Failed(Rejected(
                    "A schedule that is switched on needs a cron expression."))
                : ApiOutcome<ScanSchedule>.Success(ScanSchedule.Disabled);
        }

        try
        {
            return ApiOutcome<ScanSchedule>.Success(new ScanSchedule
            {
                IsEnabled = request.IsEnabled,
                Expression = CronExpression.Parse(request.CronExpression),
            });
        }
        catch (ArgumentException exception)
        {
            return ApiOutcome<ScanSchedule>.Failed(Rejected(exception.Message));
        }
    }

    private static ApiFailure Rejected(string detail) => ApiProblems.BadRequest(
        ApiErrorCodes.InvalidSchedule,
        detail);

    private static ApiFailure ProjectNotFound() => ApiProblems.NotFound(
        ApiErrorCodes.ProjectNotFound,
        "The requested project does not exist.");
}
