using System.Text.Json;
using Velopack;
using Velopack.Sources;

namespace App.GitHealth.Api.Features.Updates;

/// <summary>
/// Delta updates served by the project's GitHub releases. Registered only when the
/// native launcher is active on Windows or macOS.
/// </summary>
internal sealed class VelopackUpdateService : IUpdateService
{
    private const string RepositoryUrl = "https://github.com/LINDECKER-Charles/App.GitHealth";

    /// <summary>
    /// Pre-releases stay out of the stream: a version published for validation must not
    /// reach the installations that follow the released versions.
    /// </summary>
    private const bool IncludePrereleases = false;

    private static readonly Action<ILogger, string, Exception?> LogCheckFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, nameof(VelopackUpdateService)),
            "The update check failed: {Reason}");

    private readonly ILogger<VelopackUpdateService> _logger;
    private readonly UpdateManager _manager;
    private UpdateInfo? _downloaded;

    public VelopackUpdateService(ILogger<VelopackUpdateService> logger)
        : this(
            new UpdateManager(new GithubSource(
                RepositoryUrl,
                accessToken: null,
                prerelease: IncludePrereleases)),
            logger)
    {
    }

    internal VelopackUpdateService(UpdateManager manager, ILogger<VelopackUpdateService> logger)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(logger);
        _manager = manager;
        _logger = logger;
    }

    public async Task<UpdateStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        // Outside a managed installation — portable archive, run from the publication
        // folder — any query to the source would throw NotInstalledException.
        if (!_manager.IsInstalled)
        {
            return UpdateStatus.Unsupported;
        }

        var currentVersion = _manager.CurrentVersion?.ToString();
        var check = await CheckAsync(cancellationToken);
        if (!check.HasSucceeded)
        {
            return UpdateStatus.For(UpdateAvailability.Unknown, currentVersion);
        }

        return check.Update is { } update
            ? UpdateStatus.For(
                UpdateAvailability.Available,
                currentVersion,
                update.TargetFullRelease.Version.ToString())
            : UpdateStatus.For(UpdateAvailability.UpToDate, currentVersion);
    }

    public async Task<bool> DownloadAsync(CancellationToken cancellationToken)
    {
        if (!_manager.IsInstalled)
        {
            return false;
        }

        var check = await CheckAsync(cancellationToken);
        if (check.Update is not { } update)
        {
            return false;
        }

        await _manager.DownloadUpdatesAsync(
            update,
            progress: null,
            cancelToken: cancellationToken);
        _downloaded = update;
        return true;
    }

    public void ApplyAndRestart()
    {
        var update = _downloaded ?? throw new InvalidOperationException(
            "No update was downloaded.");
        _manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
    }

    /// <summary>
    /// Queries the release source. An unreachable, offline or quota-limited repository is
    /// not an application failure: the failure is reported, not propagated.
    /// </summary>
    private async Task<(bool HasSucceeded, UpdateInfo? Update)> CheckAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return (true, await _manager.CheckForUpdatesAsync().WaitAsync(cancellationToken));
        }
        catch (Exception exception) when (IsSourceUnreachable(exception, cancellationToken))
        {
            LogCheckFailed(_logger, exception.Message, exception);
            return (false, null);
        }
    }

    private static bool IsSourceUnreachable(
        Exception exception,
        CancellationToken cancellationToken) => !cancellationToken.IsCancellationRequested
        && exception is HttpRequestException
            or JsonException
            or IOException
            or TimeoutException
            or OperationCanceledException;
}
