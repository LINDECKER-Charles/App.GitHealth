using System.Runtime.InteropServices;

namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>
/// Rebranche la sortie standard sur le terminal appelant, quand il y en a un.
/// </summary>
/// <remarks>
/// L'exécutable est un programme de sous-système fenêtré : lancé au double-clic, il
/// n'ouvre aucune console. Windows ne lui en attache alors aucune non plus lorsqu'il est
/// lancé depuis un terminal, et l'aide comme les diagnostics de démarrage
/// disparaîtraient. Ce rattachement les rend de nouveau lisibles sans jamais faire
/// apparaître de fenêtre.
/// </remarks>
internal static class TerminalOutput
{
    private const int ParentProcess = -1;
    private const int StandardOutputHandle = -11;

    public static void AttachToCallingTerminal()
    {
        if (!OperatingSystem.IsWindows() || HasInheritedStandardOutput())
        {
            return;
        }

        if (AttachConsole(ParentProcess) != 0)
        {
            RebindStandardStreams();
        }
    }

    /// <summary>
    /// Une sortie déjà héritée — un tube de redirection, typiquement — ne doit pas être
    /// touchée : le rattachement remplacerait les descripteurs standards et le lecteur du
    /// tube ne recevrait plus rien. C'est le cas des smoke tests et des tests bout en bout.
    /// </summary>
    private static bool HasInheritedStandardOutput()
    {
        var handle = GetStdHandle(StandardOutputHandle);
        return handle != IntPtr.Zero && handle != InvalidHandle;
    }

    /// <summary>
    /// <see cref="Console" /> met ses écrivains en cache au premier accès : sans ce
    /// remplacement, ils continueraient d'écrire dans le vide d'avant le rattachement.
    /// </summary>
    private static void RebindStandardStreams()
    {
        Console.SetOut(AutoFlushing(Console.OpenStandardOutput()));
        Console.SetError(AutoFlushing(Console.OpenStandardError()));
    }

    private static StreamWriter AutoFlushing(Stream stream) => new(stream)
    {
        AutoFlush = true,
    };

    private static IntPtr InvalidHandle => new(-1);

    // Signatures entierement blittables : aucun marshalling, donc aucun code non
    // securise a activer sur le projet pour ces deux appels.
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int AttachConsole(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int handleId);
}
