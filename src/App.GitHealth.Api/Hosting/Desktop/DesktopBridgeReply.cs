namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>Reply sent back to the page, carrying the id of the original request.</summary>
internal sealed record DesktopBridgeReply
{
    public required string Id { get; init; }

    public required string Kind { get; init; }

    /// <summary>Chosen path, or <see langword="null" /> when the user cancelled.</summary>
    public string? Path { get; init; }
}
