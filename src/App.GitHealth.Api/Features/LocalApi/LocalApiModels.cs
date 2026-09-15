namespace App.GitHealth.Api.Features.LocalApi;

/// <summary>Where the listener actually is, as opposed to what the user asked for.</summary>
internal enum LocalApiStatus
{
    /// <summary>Nothing is listening, and nothing was asked to.</summary>
    Closed,

    /// <summary>The port is open and answering.</summary>
    Listening,

    /// <summary>The access is on, but the port could not be taken. The reason says why.</summary>
    Failed,
}

/// <summary>What the settings screen sends when the user changes the access.</summary>
internal sealed record LocalApiUpdateRequest
{
    public required bool IsEnabled { get; init; }

    public required int Port { get; init; }
}

/// <summary>
/// The access as the interface shows it. It never carries the token: that one leaves exactly
/// once, in the answer to the call that issued it.
/// </summary>
internal sealed record LocalApiStateResponse
{
    public required bool IsEnabled { get; init; }

    /// <summary>Port the user asked for, which is not yet the one in use on a failure.</summary>
    public required int Port { get; init; }

    public required string Status { get; init; }

    /// <summary>Address to hand to an orchestrator; absent unless the port is open.</summary>
    public string? Address { get; init; }

    /// <summary>Why the port could not be taken. Absent when nothing went wrong.</summary>
    public string? FailureMessage { get; init; }

    public required bool HasToken { get; init; }

    public string? TokenPrefix { get; init; }

    public DateTimeOffset? TokenIssuedAtUtc { get; init; }
}

/// <summary>The one answer a token ever appears in.</summary>
internal sealed record LocalApiTokenResponse
{
    public required string Token { get; init; }

    public required LocalApiStateResponse Access { get; init; }
}
