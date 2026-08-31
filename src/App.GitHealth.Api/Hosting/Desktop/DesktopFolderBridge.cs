using System.Runtime.InteropServices;
using System.Text.Json;
using Photino.NET;

namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>
/// Message bridge between the page and the host, limited to opening the native folder
/// dialog. The HTML folder browser stays served by the API: this bridge only replaces
/// it when the window exists.
/// </summary>
internal static class DesktopFolderBridge
{
    /// <summary>Only message kind accepted; everything else is silently ignored.</summary>
    public const string PickFolderKind = "pickFolder";

    private const string FolderDialogTitle = "Choose a folder";

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
    /// Called on the window thread, the very one that pumps the event loop: the native
    /// dialog therefore opens with no marshalling and no deadlock.
    /// </summary>
    private static void OnMessageReceived(object? sender, string message)
    {
        if (sender is not PhotinoWindow window || ReadRequestId(message) is not { } requestId)
        {
            return;
        }

        var reply = new DesktopBridgeReply
        {
            Id = requestId,
            Kind = PickFolderKind,
            Path = PickFolder(window),
        };
        window.SendWebMessage(JsonSerializer.Serialize(reply, MessageFormat));
    }

    /// <summary>
    /// Id of a usable request — identified and recognised — otherwise
    /// <see langword="null" />. An unreadable message is not an error: the page may
    /// emit others, and the host has no business judging them.
    /// </summary>
    private static string? ReadRequestId(string message)
    {
        DesktopBridgeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<DesktopBridgeRequest>(message, MessageFormat);
        }
        catch (JsonException)
        {
            return null;
        }

        return request is { Kind: PickFolderKind, Id: { Length: > 0 } id } ? id : null;
    }

    private static string? PickFolder(PhotinoWindow window)
    {
        try
        {
            var selection = window.ShowOpenFolder(
                FolderDialogTitle,
                defaultPath: null,
                multiSelect: false);
            return selection.Length == 0 ? null : selection[0];
        }
        catch (Exception exception) when (exception is ExternalException
            or InvalidOperationException or NotSupportedException or IOException)
        {
            // A dialog failure must not kill the window: the page receives a
            // cancellation and its HTML folder browser stays available.
            Console.Error.WriteLine(
                $"The native folder dialog failed: {exception.Message}");
            return null;
        }
    }
}
