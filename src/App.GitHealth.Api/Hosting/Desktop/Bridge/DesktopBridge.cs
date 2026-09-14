using System.Text.Json;
using Photino.NET;

namespace App.GitHealth.Api.Hosting.Desktop.Bridge;

/// <summary>
/// Message bridge between the page and the host. It serves what a webview cannot do on
/// its own — the native folder dialog, opening an address outside the window, writing a
/// file to disk — and nothing more.
/// </summary>
internal static class DesktopBridge
{
    private static readonly JsonSerializerOptions MessageFormat = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static PhotinoWindow Register(PhotinoWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return window.RegisterWebMessageReceivedHandler(OnMessageReceived);
    }

    /// <summary>
    /// Called on the window thread, the very one that pumps the event loop: a native
    /// dialog therefore opens with no marshalling and no deadlock.
    /// </summary>
    private static void OnMessageReceived(object? sender, string message)
    {
        var request = ReadRequest(message);
        if (sender is not PhotinoWindow window || request?.Id is not { Length: > 0 } id)
        {
            return;
        }

        if (Handle(window, request, id) is { } reply)
        {
            window.SendWebMessage(JsonSerializer.Serialize(reply, MessageFormat));
        }
    }

    /// <returns>
    /// The reply owed to the page, or <see langword="null" /> for a kind the host does not
    /// serve — the page may emit others, and the host has no business judging them.
    /// </returns>
    private static DesktopBridgeReply? Handle(
        PhotinoWindow window,
        DesktopBridgeRequest request,
        string id) => request.Kind switch
        {
            DesktopFolderPicker.Kind => DesktopFolderPicker.Handle(window, id),
            DesktopExternalLink.Kind => DesktopExternalLink.Handle(request.Url, id),
            DesktopFileSaver.Kind => DesktopFileSaver.Handle(request, id),
            _ => null,
        };

    /// <summary>
    /// The request as the page wrote it, otherwise <see langword="null" />: an unreadable
    /// message is not an error, it is simply not for the host.
    /// </summary>
    private static DesktopBridgeRequest? ReadRequest(string message)
    {
        try
        {
            return JsonSerializer.Deserialize<DesktopBridgeRequest>(message, MessageFormat);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
