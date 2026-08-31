namespace App.GitHealth.Api.Features.Updates;

/// <summary>What the application can say about an update, from its point of view.</summary>
internal enum UpdateAvailability
{
    /// <summary>
    /// No in-app update: Docker, browser, Linux, or portable copy.
    /// </summary>
    Unsupported,

    /// <summary>The installed version is the latest released.</summary>
    UpToDate,

    /// <summary>
    /// The release source is unreachable — offline, quota reached, repository
    /// unavailable. The application can neither offer nor rule out an update.
    /// </summary>
    Unknown,

    /// <summary>A newer version is released and installable.</summary>
    Available,
}
