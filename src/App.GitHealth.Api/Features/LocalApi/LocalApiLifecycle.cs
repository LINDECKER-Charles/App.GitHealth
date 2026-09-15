namespace App.GitHealth.Api.Features.LocalApi;

/// <summary>
/// Ties the access to the application's own life: it reopens at boot what the user left open,
/// and closes the port when GitHealth stops. Registered after persistence on purpose — a
/// caller must never reach a database whose migrations have not run yet.
/// </summary>
internal sealed partial class LocalApiLifecycle(
    LocalApiService access,
    LocalApiHost host,
    ILogger<LocalApiLifecycle> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await access.RestoreAsync(cancellationToken);
        if (host.Status == LocalApiStatus.Failed)
        {
            LogFailed(logger, host.FailureMessage ?? "no reason given");
        }
        else if (host.BoundPort is { } port)
        {
            LogListening(logger, port);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        host.CloseAsync(cancellationToken);

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Information,
        Message = "The local API is listening on 127.0.0.1:{Port}.")]
    private static partial void LogListening(ILogger logger, int port);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Warning,
        Message = "The local API could not be reopened: {Reason}")]
    private static partial void LogFailed(ILogger logger, string reason);
}
