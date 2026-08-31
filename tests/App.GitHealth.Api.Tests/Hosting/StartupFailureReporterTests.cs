using App.GitHealth.Api.Hosting;
using App.GitHealth.Api.Persistence.Services;

namespace App.GitHealth.Api.Tests.Hosting;

public sealed class StartupFailureReporterTests
{
    [Fact]
    public void HelpDocumentsEveryLauncherOption()
    {
        Assert.Contains("--repo", StartupFailureReporter.HelpText, StringComparison.Ordinal);
        Assert.Contains("--port", StartupFailureReporter.HelpText, StringComparison.Ordinal);
        Assert.Contains("--data-dir", StartupFailureReporter.HelpText, StringComparison.Ordinal);
        Assert.Contains("--git-path", StartupFailureReporter.HelpText, StringComparison.Ordinal);
        Assert.Contains("--no-browser", StartupFailureReporter.HelpText, StringComparison.Ordinal);
        Assert.Contains("--no-window", StartupFailureReporter.HelpText, StringComparison.Ordinal);
        Assert.Contains("--help", StartupFailureReporter.HelpText, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidArgumentsDirectUsersToHelp()
    {
        var message = StartupFailureReporter.InvalidArguments("Invalid port.");

        Assert.Contains("Invalid port", message, StringComparison.Ordinal);
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
        Assert.Contains("No loopback port", automatic, StringComparison.Ordinal);
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
        Assert.Contains("invalid or unreachable", invalid, StringComparison.Ordinal);
        Assert.Contains(path, inUse, StringComparison.Ordinal);
        Assert.Contains("another GitHealth instance", inUse, StringComparison.Ordinal);
    }

    [Fact]
    public void GitFailureProvidesAnActionableInstruction()
    {
        var message = StartupFailureReporter.GitUnavailable("Additional diagnostic.");

        Assert.Contains("Install Git", message, StringComparison.Ordinal);
        Assert.Contains("Additional diagnostic", message, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnoseRecognisesACauseBuriedInTheExceptionChain()
    {
        const string databasePath = "D:/data/githealth.db";
        var wrapped = new InvalidOperationException(
            "Startup failed.",
            new DatabaseInUseException(databasePath, new IOException()));

        var message = StartupFailureReporter.Diagnose(wrapped, port: 5187, databasePath);

        Assert.Equal(StartupFailureReporter.DatabaseInUse(databasePath), message);
    }

    [Fact]
    public void DiagnoseMapsADirectoryFailureToItsParentDirectory()
    {
        var databasePath = Path.Combine("D:", "data", "githealth.db");

        var message = StartupFailureReporter.Diagnose(
            new UnauthorizedAccessException(),
            port: 5187,
            databasePath);

        Assert.Equal(
            StartupFailureReporter.DataDirectoryUnavailable(
                Path.GetDirectoryName(databasePath)!),
            message);
    }

    [Fact]
    public void DiagnoseFallsBackToTheUnexpectedMessage()
    {
        var message = StartupFailureReporter.Diagnose(
            new InvalidProgramException(),
            port: 5187,
            databasePath: "D:/data/githealth.db");

        Assert.Equal(StartupFailureReporter.Unexpected(), message);
    }

    [Fact]
    public void WriteAppendsTheMessageToErrorOutput()
    {
        using var output = new StringWriter();

        StartupFailureReporter.Write(output, "Controlled failure.");

        Assert.Equal($"Controlled failure.{Environment.NewLine}", output.ToString());
    }
}
