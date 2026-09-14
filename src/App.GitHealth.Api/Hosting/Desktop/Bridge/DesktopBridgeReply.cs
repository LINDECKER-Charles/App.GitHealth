namespace App.GitHealth.Api.Hosting.Desktop.Bridge;

/// <summary>Reply sent back to the page, carrying the id of the original request.</summary>
internal sealed record DesktopBridgeReply
{
    public required string Id { get; init; }

    public required string Kind { get; init; }

    /// <summary>
    /// Path chosen or written, or <see langword="null" /> when the user cancelled and
    /// when nothing reached the disk.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>Whether the host served the request; the page falls back otherwise.</summary>
    public bool IsHandled { get; init; }
}
