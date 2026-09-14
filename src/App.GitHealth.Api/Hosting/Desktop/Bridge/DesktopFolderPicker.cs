using System.Runtime.InteropServices;
using Photino.NET;

namespace App.GitHealth.Api.Hosting.Desktop.Bridge;

/// <summary>
/// Native folder dialog. The HTML folder browser stays served by the API: this handler
/// only replaces it when the window exists.
/// </summary>
internal static class DesktopFolderPicker
{
    public const string Kind = "pickFolder";

    private const string FolderDialogTitle = "Choose a folder";

    public static DesktopBridgeReply Handle(PhotinoWindow window, string id)
    {
        var path = Pick(window);
        return new DesktopBridgeReply
        {
            Id = id,
            Kind = Kind,
            Path = path,
            IsHandled = path is not null,
        };
    }

    private static string? Pick(PhotinoWindow window)
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
