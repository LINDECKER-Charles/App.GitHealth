using System.Reflection;
using App.GitHealth.Api.Features;
using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Git;
using App.GitHealth.Api.Hosting;
using App.GitHealth.Api.Persistence;
using App.GitHealth.Api.Persistence.Services;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;

var parseResult = LauncherOptionsParser.Parse(args);
if (!parseResult.IsSuccess)
{
    StartupFailureReporter.Write(
        Console.Error,
        StartupFailureReporter.InvalidArguments(parseResult.ErrorMessage!));
    Environment.ExitCode = StartupFailureReporter.FailureExitCode;
    return;
}

var launcherOptions = parseResult.Options!;
if (launcherOptions.ShowHelp)
{
    Console.WriteLine(StartupFailureReporter.HelpText);
    return;
}

var isDirectLaunch = Assembly.GetEntryAssembly() == typeof(Program).Assembly;
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = launcherOptions.HostArguments.ToArray(),
    ContentRootPath = isDirectLaunch ? AppContext.BaseDirectory : null,
});
try
{
    ApplyLauncherConfiguration(builder.Configuration, launcherOptions);
}
catch (Exception exception) when (IsInvalidPath(exception))
{
    StartupFailureReporter.Write(
        Console.Error,
        StartupFailureReporter.InvalidArguments(exception.Message));
    Environment.ExitCode = StartupFailureReporter.FailureExitCode;
    return;
}

var useNativeLauncher = isDirectLaunch && !IsContainer();
if (useNativeLauncher)
{
    if (LauncherBindingGuard.HasConfiguredKestrelEndpoints(builder.Configuration))
    {
        StartupFailureReporter.Write(
            Console.Error,
            StartupFailureReporter.KestrelEndpointsNotAllowed());
        Environment.ExitCode = StartupFailureReporter.FailureExitCode;
        return;
    }

    builder.WebHost.ConfigureKestrel(server =>
        server.Listen(LauncherOptions.ListenAddress, launcherOptions.Port));
}

builder.Services.AddOpenApi();
builder.Services.AddGitScanner(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddGitHealthApi(builder.Configuration);

await using var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = GitHealthResponseWriter.WriteAsync,
});
app.MapOpenApi();
app.MapGitHealthApi();
app.MapFallback("/api/{**path}", () => ApiProblems.Result(ApiProblems.NotFound(
    ApiErrorCodes.EndpointNotFound,
    "La route API demandée n’existe pas.")));
app.MapFallbackToFile("index.html");

if (isDirectLaunch)
{
    await RunDirectAsync(app, launcherOptions, useNativeLauncher);
}
else
{
    await app.RunAsync();
}

static void ApplyLauncherConfiguration(
    ConfigurationManager configuration,
    LauncherOptions options)
{
    ApplyRepositoryConfiguration(configuration, options.RepositoryPath);
    ApplyDataConfiguration(configuration, options.DataDirectory);
}

static void ApplyRepositoryConfiguration(
    ConfigurationManager configuration,
    string? repositoryPath)
{
    if (!string.IsNullOrWhiteSpace(repositoryPath))
    {
        configuration["GitHealth:InitialRepositoryPath"] = Path.GetFullPath(
            repositoryPath);
    }
}

static void ApplyDataConfiguration(
    ConfigurationManager configuration,
    string? requestedDataDirectory)
{
    var configuredDataDirectory = requestedDataDirectory
        ?? configuration["GitHealth:DataDirectory"];
    var configuredDatabase = configuration["Persistence:DatabasePath"];
    var usesLegacyDefault = string.Equals(
        configuredDatabase,
        "data/githealth.db",
        StringComparison.OrdinalIgnoreCase);
    if (configuredDataDirectory is null
        && !string.IsNullOrWhiteSpace(configuredDatabase)
        && !usesLegacyDefault)
    {
        return;
    }

    var dataDirectory = DataDirectoryResolver.ForCurrentPlatform()
        .Resolve(configuredDataDirectory);
    configuration["GitHealth:DataDirectory"] = dataDirectory;
    configuration["Persistence:DatabasePath"] = Path.Combine(
        dataDirectory,
        "githealth.db");
}

static async Task RunDirectAsync(
    WebApplication app,
    LauncherOptions options,
    bool useNativeLauncher)
{
    try
    {
        if (useNativeLauncher)
        {
            await RunNativeAsync(app, options);
        }
        else
        {
            await app.RunAsync();
        }
    }
    catch (Exception exception)
    {
        ReportStartupFailure(exception, app, options);
        Environment.ExitCode = StartupFailureReporter.FailureExitCode;
    }
}

static async Task RunNativeAsync(WebApplication app, LauncherOptions options)
{
    await app.StartAsync();
    var address = LauncherOptions.CreateApplicationAddress(BoundPort(app));
    Console.WriteLine($"GitHealth est disponible sur {address}");
    if (options.ShouldOpenBrowser)
    {
        var warning = new SystemBrowserLauncher().Open(address);
        if (warning is not null)
        {
            Console.Error.WriteLine(warning);
        }
    }

    await app.WaitForShutdownAsync();
}

static int BoundPort(WebApplication app)
{
    var server = app.Services.GetRequiredService<IServer>();
    var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
    var address = addresses?.Select(value => new Uri(value)).SingleOrDefault();
    return address?.Port
        ?? throw new InvalidOperationException("Le port attribué est introuvable.");
}

static void ReportStartupFailure(
    Exception exception,
    WebApplication app,
    LauncherOptions options)
{
    var databasePath = app.Configuration["Persistence:DatabasePath"] ?? "githealth.db";
    var message = StartupFailureMessage(exception, options, databasePath);
    StartupFailureReporter.Write(Console.Error, message);
}

static string StartupFailureMessage(
    Exception exception,
    LauncherOptions options,
    string databasePath)
{
    if (FindException<DatabaseInUseException>(exception) is { } inUse)
    {
        return StartupFailureReporter.DatabaseInUse(inUse.DatabasePath);
    }

    if (FindException<AddressInUseException>(exception) is not null)
    {
        return StartupFailureReporter.PortUnavailable(options.Port);
    }

    if (FindException<SqliteException>(exception) is not null)
    {
        return StartupFailureReporter.DatabaseUnavailable(databasePath);
    }

    if (FindException<UnauthorizedAccessException>(exception) is not null
        || FindException<IOException>(exception) is not null)
    {
        return StartupFailureReporter.DataDirectoryUnavailable(
            Path.GetDirectoryName(databasePath) ?? databasePath);
    }

    return StartupFailureReporter.Unexpected();
}

static TException? FindException<TException>(Exception exception)
    where TException : Exception
{
    for (var current = exception; current is not null; current = current.InnerException)
    {
        if (current is TException match)
        {
            return match;
        }
    }

    return null;
}

static bool IsContainer() => string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);

static bool IsInvalidPath(Exception exception) => exception is ArgumentException
    or NotSupportedException
    or PathTooLongException;

public partial class Program;
