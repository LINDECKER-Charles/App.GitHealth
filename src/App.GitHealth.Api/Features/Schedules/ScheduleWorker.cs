using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.Schedules;

/// <summary>
/// The tick behind every schedule. It holds no state of its own: what is due is decided from
/// the database on each pass, so a schedule written a second ago is honoured, and one deleted
/// a second ago is not.
/// </summary>
/// <remarks>
/// A firing only happens while GitHealth is running. That is not a limitation to work around
/// but the shape of the product: it is a local application, not a service, and it does not
/// install anything that outlives its own window.
/// </remarks>
internal sealed partial class ScheduleWorker(
    ScheduledScanRunner runner,
    IOptions<ScheduleOptions> options,
    ILogger<ScheduleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            LogDisabled(logger);
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.TickSeconds);
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await TickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// A failed pass is reported and the loop goes on. The alternative — letting the exception
    /// end the worker — would take every schedule of the installation down with it, silently.
    /// </summary>
    private async Task TickAsync(CancellationToken stoppingToken)
    {
        try
        {
            await runner.RunDueAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LogTickFailed(logger, exception);
        }
    }

    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Information,
        Message = "Scheduled scanning is switched off for this installation.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(
        EventId = 2104,
        Level = LogLevel.Error,
        Message = "A scheduled scan pass failed.")]
    private static partial void LogTickFailed(ILogger logger, Exception exception);
}
