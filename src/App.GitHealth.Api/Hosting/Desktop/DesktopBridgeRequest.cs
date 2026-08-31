namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>
/// Request sent by the page to the host. The content comes from the webview: it is
/// treated as untrusted input, never as a command.
/// </summary>
internal sealed record DesktopBridgeRequest
{
    /// <summary>Correlates the reply with the request; the bridge is asynchronous.</summary>
    public string? Id { get; init; }

    public string? Kind { get; init; }
}
