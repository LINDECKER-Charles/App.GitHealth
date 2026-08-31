using System.ComponentModel;
using System.Diagnostics;
using System.Security;

namespace App.GitHealth.Api.Hosting;

internal sealed class SystemBrowserLauncher
{
    private readonly Action<ProcessStartInfo> _start;

    public SystemBrowserLauncher()
        : this(StartProcess)
    {
    }

    internal SystemBrowserLauncher(Action<ProcessStartInfo> start)
    {
        ArgumentNullException.ThrowIfNull(start);
        _start = start;
    }

    public string? Open(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!address.IsAbsoluteUri || !IsHttp(address))
        {
            return "The GitHealth address cannot be opened in a browser.";
        }

        try
        {
            _start(CreateStartInfo(address));
            return null;
        }
        catch (Exception exception) when (exception is Win32Exception
            or InvalidOperationException or NotSupportedException or SecurityException)
        {
            return $"The browser could not be opened. Open {address} manually.";
        }
    }

    private static ProcessStartInfo CreateStartInfo(Uri address) => new()
    {
        FileName = address.AbsoluteUri,
        UseShellExecute = true,
    };

    private static bool IsHttp(Uri address) => address.Scheme is "http" or "https";

    private static void StartProcess(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The browser did not start.");
    }
}
