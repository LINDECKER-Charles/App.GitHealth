using System.Text.Json;
using Velopack;
using Velopack.Sources;

namespace App.GitHealth.Api.Features.Updates;

/// <summary>
/// Mises à jour delta servies par les releases GitHub du projet. Enregistrée uniquement
/// quand le lanceur natif est actif sur Windows ou macOS.
/// </summary>
internal sealed class VelopackUpdateService : IUpdateService
{
    private const string RepositoryUrl = "https://github.com/LINDECKER-Charles/App.GitHealth";

    /// <summary>
    /// Le produit ne publie encore que des versions candidates : les ignorer reviendrait
    /// à ne jamais proposer de mise à jour.
    /// </summary>
    private const bool IncludePrereleases = true;

    private static readonly Action<ILogger, string, Exception?> LogCheckFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, nameof(VelopackUpdateService)),
            "La recherche de mise à jour a échoué : {Reason}");

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
        // Hors installation gérée — archive portable, exécution depuis le dossier de
        // publication — toute interrogation de la source lèverait NotInstalledException.
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
            "Aucune mise à jour n'a été téléchargée.");
        _manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
    }

    /// <summary>
    /// Interroge la source des releases. Un dépôt injoignable, hors ligne ou limité en
    /// quota n'est pas une panne de l'application : l'échec est rapporté, pas propagé.
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
