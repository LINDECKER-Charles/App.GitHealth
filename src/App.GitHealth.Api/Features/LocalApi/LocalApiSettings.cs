namespace App.GitHealth.Api.Features.LocalApi;

/// <summary>
/// What the user decided about the local API, as it is written to disk. It stays out of the
/// SQLite database on purpose: that file is exported and copied around, and the fingerprint
/// of a live token has no business travelling with the captures.
/// </summary>
internal sealed record LocalApiSettings
{
    /// <summary>
    /// Below this, a port belongs to the system on Unix and needs privileges GitHealth does
    /// not have.
    /// </summary>
    public const int MinimumPort = 1024;

    public const int MaximumPort = 65535;

    /// <summary>
    /// Registered range, below the ephemeral window every platform draws from, so the
    /// listener does not fight an outbound connection for its own port.
    /// </summary>
    public const int DefaultPort = 7823;

    public static LocalApiSettings Closed { get; } = new();

    public bool IsEnabled { get; init; }

    public int Port { get; init; } = DefaultPort;

    /// <summary>Base64 SHA-256 of the token. The token itself is never stored.</summary>
    public string? TokenFingerprint { get; init; }

    /// <summary>Head of the token, enough to tell which one an orchestrator holds.</summary>
    public string? TokenPrefix { get; init; }

    public DateTimeOffset? TokenIssuedAtUtc { get; init; }

    public bool HasToken => !string.IsNullOrEmpty(TokenFingerprint);

    public static bool IsValidPort(int port) => port is >= MinimumPort and <= MaximumPort;
}
