using System.Runtime.InteropServices;
using System.Text.Json;
using Photino.NET;

namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>
/// Pont de messages entre la page et l'hôte, limité à l'ouverture du dialogue de dossier
/// natif. Le navigateur de dossiers HTML reste servi par l'API : ce pont ne le remplace
/// que lorsque la fenêtre existe.
/// </summary>
internal static class DesktopFolderBridge
{
    /// <summary>Seul type de message accepté ; tout le reste est ignoré en silence.</summary>
    public const string PickFolderKind = "pickFolder";

    private const string FolderDialogTitle = "Choisir un dossier";

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
    /// Appelé sur le thread de la fenêtre, celui-là même qui pompe la boucle
    /// d'évènements : le dialogue natif s'ouvre donc sans marshalling ni interblocage.
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
    /// Identifiant d'une demande exploitable — identifiée et reconnue —, sinon
    /// <see langword="null" />. Un message illisible n'est pas une erreur : la page peut
    /// en émettre d'autres, et l'hôte n'a pas à en juger.
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
            // Une panne du dialogue ne doit pas tuer la fenêtre : la page reçoit une
            // annulation et son navigateur de dossiers HTML reste disponible.
            Console.Error.WriteLine(
                $"Le dialogue de dossier natif a échoué : {exception.Message}");
            return null;
        }
    }
}
