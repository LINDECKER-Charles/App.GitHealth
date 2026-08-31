using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using DiagnosticsProcess = System.Diagnostics.Process;

if (args.Length < 2)
{
    return 2;
}

PublishProcessId(args[1]);
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
    ?? throw new InvalidOperationException("The child process did not start.");
await child.WaitForExitAsync();
return child.ExitCode;

// The probe waits for the file to appear before reading the PID from it: writing in place
// would make the file visible before it is filled in, exposing empty or truncated content.
// The PID is therefore written next to it, then published by a rename, which is atomic
// within a single directory.
static void PublishProcessId(string path)
{
    var stagingPath = path + ".tmp";
    File.WriteAllText(
        stagingPath,
        Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
    File.Move(stagingPath, path, overwrite: true);
}
