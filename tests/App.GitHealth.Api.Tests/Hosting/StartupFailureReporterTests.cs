using App.GitHealth.Api.Hosting;

namespace App.GitHealth.Api.Tests.Hosting;

public sealed class StartupFailureReporterTests
{
    [Fact]
    public void HelpDocumentsEveryLauncherOption()
    {
        Assert.Contains("--repo", StartupFailureReporter.HelpText, StringComparison.Ordinal);
        Assert.Contains("--port", StartupFailureReporter.HelpText, StringComparison.Ordinal);
        Assert.Contains("--data-dir", StartupFailureReporter.HelpText, StringComparison.Ordinal);
        Assert.Contains("--no-browser", StartupFailureReporter.HelpText, StringComparison.Ordinal);
        Assert.Contains("--help", StartupFailureReporter.HelpText, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidArgumentsDirectUsersToHelp()
    {
        var message = StartupFailureReporter.InvalidArguments("Port incorrect.");

        Assert.Contains("Port incorrect", message, StringComparison.Ordinal);
        Assert.Contains("--help", message, StringComparison.Ordinal);
        Assert.Equal(1, StartupFailureReporter.FailureExitCode);
    }

    [Fact]
    public void PortFailureDistinguishesRequestedAndAutomaticPorts()
    {
        var requested = StartupFailureReporter.PortUnavailable(5187);
        var automatic = StartupFailureReporter.PortUnavailable(LauncherOptions.AutomaticPort);

        Assert.Contains("5187", requested, StringComparison.Ordinal);
        Assert.Contains("--port", requested, StringComparison.Ordinal);
        Assert.Contains("Aucun port loopback", automatic, StringComparison.Ordinal);
    }

    [Fact]
    public void KestrelEndpointFailurePreservesLoopbackBinding()
    {
        var message = StartupFailureReporter.KestrelEndpointsNotAllowed();

        Assert.Contains("Kestrel:Endpoints", message, StringComparison.Ordinal);
        Assert.Contains("loopback", message, StringComparison.Ordinal);
        Assert.Contains("--port", message, StringComparison.Ordinal);
    }

    [Fact]
    public void DatabaseFailuresDistinguishInvalidAndAlreadyUsedFiles()
    {
        const string path = "D:/data/githealth.db";

        var invalid = StartupFailureReporter.DatabaseUnavailable(path);
        var inUse = StartupFailureReporter.DatabaseInUse(path);

        Assert.Contains(path, invalid, StringComparison.Ordinal);
        Assert.Contains("invalide ou inaccessible", invalid, StringComparison.Ordinal);
        Assert.Contains(path, inUse, StringComparison.Ordinal);
        Assert.Contains("autre instance", inUse, StringComparison.Ordinal);
    }

    [Fact]
    public void GitFailureProvidesAnActionableInstruction()
    {
        var message = StartupFailureReporter.GitUnavailable("Diagnostic complémentaire.");

        Assert.Contains("Installez Git", message, StringComparison.Ordinal);
        Assert.Contains("Diagnostic complémentaire", message, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteAppendsTheMessageToErrorOutput()
    {
        using var output = new StringWriter();

        StartupFailureReporter.Write(output, "Échec contrôlé.");

        Assert.Equal($"Échec contrôlé.{Environment.NewLine}", output.ToString());
    }
}
