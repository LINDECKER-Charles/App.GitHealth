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
    ?? throw new InvalidOperationException("Le processus enfant n’a pas démarré.");
await child.WaitForExitAsync();
return child.ExitCode;

// La sonde attend l'apparition du fichier pour en lire le PID : une écriture directe le
// rendrait visible avant d'être renseigné, exposant un contenu vide ou tronqué. Le PID est
// donc écrit à côté puis publié par un renommage, atomique dans un même répertoire.
static void PublishProcessId(string path)
{
    var stagingPath = path + ".tmp";
    File.WriteAllText(
        stagingPath,
        Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
    File.Move(stagingPath, path, overwrite: true);
}
