using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Hosting;

namespace App.GitHealth.Api.Features.LocalApi;

/// <summary>
/// The one place the access is decided. It writes what the user asked for, then makes the
/// listener agree with it — in that order, so a port that refuses to open still leaves a
/// setting the next start will honour.
/// </summary>
internal sealed class LocalApiService(LocalApiSettingsStore store, LocalApiHost host)
{
    public LocalApiStateResponse Describe() => Map(store.Read());

    public async Task<ApiOutcome<LocalApiStateResponse>> ApplyAsync(
        LocalApiUpdateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var refusal = Refuse(request, store.Read());
        if (refusal is not null)
        {
            return ApiOutcome<LocalApiStateResponse>.Failed(refusal);
        }

        var settings = store.Read() with
        {
            IsEnabled = request.IsEnabled,
            Port = request.Port,
        };
        store.Write(settings);
        await SynchronizeAsync(settings, cancellationToken);
        return ApiOutcome<LocalApiStateResponse>.Success(Map(settings));
    }

    /// <summary>
    /// Issues a token and revokes the previous one in the same move. A listener already open
    /// needs no restart: every request weighs the token against what is on disk right then.
    /// </summary>
    public LocalApiTokenResponse IssueToken()
    {
        var issued = LocalApiToken.Issue(DateTimeOffset.UtcNow);
        var settings = store.Read() with
        {
            TokenFingerprint = issued.Fingerprint,
            TokenPrefix = issued.Prefix,
            TokenIssuedAtUtc = issued.IssuedAtUtc,
        };
        store.Write(settings);
        return new LocalApiTokenResponse { Token = issued.Token, Access = Map(settings) };
    }

    /// <summary>Reopens at boot whatever the user left open when they last quit.</summary>
    public Task RestoreAsync(CancellationToken cancellationToken) =>
        SynchronizeAsync(store.Read(), cancellationToken);

    /// <summary>
    /// An access without a token would be an open port, which is the one thing this feature
    /// must never be. The interface issues one first, and hands it over as it does.
    /// </summary>
    private static ApiFailure? Refuse(LocalApiUpdateRequest request, LocalApiSettings current)
    {
        if (!LocalApiSettings.IsValidPort(request.Port))
        {
            return ApiProblems.BadRequest(
                ApiErrorCodes.LocalApiPortInvalid,
                $"The port must be between {LocalApiSettings.MinimumPort} and"
                + $" {LocalApiSettings.MaximumPort}.");
        }

        return request.IsEnabled && !current.HasToken
            ? ApiProblems.Conflict(
                ApiErrorCodes.LocalApiTokenRequired,
                "A token must be issued before the access is opened.")
            : null;
    }

    private Task SynchronizeAsync(
        LocalApiSettings settings,
        CancellationToken cancellationToken) =>
        settings.IsEnabled
            ? host.OpenAsync(settings.Port, cancellationToken)
            : host.CloseAsync(cancellationToken);

    private LocalApiStateResponse Map(LocalApiSettings settings) => new()
    {
        IsEnabled = settings.IsEnabled,
        Port = settings.Port,
        Status = host.Status.ToString(),
        Address = host.BoundPort is { } port
            ? LauncherOptions.CreateApplicationAddress(port).ToString()
            : null,
        FailureMessage = host.FailureMessage,
        HasToken = settings.HasToken,
        TokenPrefix = settings.TokenPrefix,
        TokenIssuedAtUtc = settings.TokenIssuedAtUtc,
    };
}
