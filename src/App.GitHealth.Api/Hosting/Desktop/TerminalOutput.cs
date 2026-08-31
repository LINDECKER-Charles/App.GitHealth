using System.Runtime.InteropServices;

namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>
/// Reconnects the standard output to the calling terminal, when there is one.
/// </summary>
/// <remarks>
/// The executable is a windowed-subsystem program: launched by double-click, it opens
/// no console. Windows then attaches none to it either when it is launched from a
/// terminal, and the help as well as the startup diagnostics would disappear. This
/// attachment makes them readable again without ever showing a window.
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
    /// An already inherited output — a redirection pipe, typically — must not be
    /// touched: the attachment would replace the standard descriptors and the pipe
    /// reader would receive nothing more. This is the case of the smoke tests and
    /// the end-to-end tests.
    /// </summary>
    private static bool HasInheritedStandardOutput()
    {
        var handle = GetStdHandle(StandardOutputHandle);
        return handle != IntPtr.Zero && handle != InvalidHandle;
    }

    /// <summary>
    /// <see cref="Console" /> caches its writers on first access: without this
    /// replacement, they would keep writing into the void from before the attachment.
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

    // Fully blittable signatures: no marshalling, hence no unsafe code to enable on
    // the project for these two calls.
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int AttachConsole(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int handleId);
}
