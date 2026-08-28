using System.Diagnostics;
using System.Reflection;
using DiagnosticsProcess = System.Diagnostics.Process;

if (args.Length < 2)
{
    return 2;
}

File.WriteAllText(
    args[1],
    Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
if (args[0] == "child")
{
    await Task.Delay(Timeout.InfiniteTimeSpan);
    return 0;
}

if (args[0] != "spawn" || args.Length < 3)
{
    return 3;
}

var startInfo = new ProcessStartInfo("dotnet")
{
    UseShellExecute = false,
    CreateNoWindow = true,
};
startInfo.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
startInfo.ArgumentList.Add("child");
startInfo.ArgumentList.Add(args[2]);
using var child = DiagnosticsProcess.Start(startInfo)
    ?? throw new InvalidOperationException("Le processus enfant n’a pas démarré.");
await child.WaitForExitAsync();
return child.ExitCode;
