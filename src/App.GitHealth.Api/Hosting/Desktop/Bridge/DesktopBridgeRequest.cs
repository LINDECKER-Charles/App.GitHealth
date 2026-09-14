namespace App.GitHealth.Api.Hosting.Desktop.Bridge;

/// <summary>
/// Request sent by the page to the host. The content comes from the webview: it is
/// treated as untrusted input, never as a command.
/// </summary>
internal sealed record DesktopBridgeRequest
{
    /// <summary>Correlates the reply with the request; the bridge is asynchronous.</summary>
    public string? Id { get; init; }

    public string? Kind { get; init; }

    /// <summary>Address to hand over to the system browser, for an external link.</summary>
    public string? Url { get; init; }

    /// <summary>Name the file is written under, for a save.</summary>
    public string? FileName { get; init; }

    /// <summary>Base64 payload, for a save: the bridge carries text and nothing else.</summary>
    public string? Contents { get; init; }
}
