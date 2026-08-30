using System.Reflection;
using App.GitHealth.Api.Features;
using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Features.Security;
using App.GitHealth.Api.Git;
using App.GitHealth.Api.Git.Process;
using App.GitHealth.Api.Hosting;
using App.GitHealth.Api.Hosting.Desktop;
using App.GitHealth.Api.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace App.GitHealth.Api;

/// <summary>
/// Point d'entrée de GitHealth.
/// </summary>
/// <remarks>
/// L'entrée est explicite et marquée <see cref="STAThreadAttribute" /> : les instructions
/// de haut niveau laissent le thread principal en apartment MTA, où WebView2 ne
/// s'initialise jamais — la fenêtre s'ouvre mais reste vide, sans exception ni journal.
/// </remarks>
public sealed partial class Program
{
    private Program()
    {
    }

    [STAThread]
    private static void Main(string[] args)
    {
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
        var builder = CreateBuilder(launcherOptions, isDirectLaunch);
        if (builder is not null)
        {
            Run(builder, launcherOptions, isDirectLaunch);
        }
    }

    private static WebApplicationBuilder? CreateBuilder(
        LauncherOptions options,
        bool isDirectLaunch)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = options.HostArguments.ToArray(),
            ContentRootPath = isDirectLaunch ? AppContext.BaseDirectory : null,
        });
        try
        {
            ApplyLauncherConfiguration(builder.Configuration, options);
            return builder;
        }
        catch (Exception exception) when (IsInvalidPath(exception))
        {
            StartupFailureReporter.Write(
                Console.Error,
                StartupFailureReporter.InvalidArguments(exception.Message));
            Environment.ExitCode = StartupFailureReporter.FailureExitCode;
            return null;
        }
    }

    private static void Run(
        WebApplicationBuilder builder,
        LauncherOptions options,
        bool isDirectLaunch)
    {
        var useNativeLauncher = isDirectLaunch && !IsContainer();
        if (useNativeLauncher && !ConfigureLoopbackBinding(builder, options))
        {
            return;
        }

        AddServices(builder);
        using var app = builder.Build();
        MapPipeline(app);
        if (isDirectLaunch)
        {
            RunDirect(app, options, useNativeLauncher);
            return;
        }

        app.RunAsync().GetAwaiter().GetResult();
    }

    private static bool ConfigureLoopbackBinding(
        WebApplicationBuilder builder,
        LauncherOptions options)
    {
        if (LauncherBindingGuard.HasConfiguredKestrelEndpoints(builder.Configuration))
        {
            StartupFailureReporter.Write(
                Console.Error,
                StartupFailureReporter.KestrelEndpointsNotAllowed());
            Environment.ExitCode = StartupFailureReporter.FailureExitCode;
            return false;
        }

        builder.WebHost.ConfigureKestrel(server =>
            server.Listen(LauncherOptions.ListenAddress, options.Port));
        return true;
    }

    private static void AddServices(WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddOpenApi();
        }

        builder.Services.AddGitScanner(builder.Configuration);
        builder.Services.AddPersistence(builder.Configuration);
        builder.Services.AddGitHealthApi(builder.Configuration);
        builder.Services.AddLocalRequestSecurity(builder.Configuration);
    }

    private static void MapPipeline(WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseLocalRequestSecurity();
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = GitHealthResponseWriter.WriteAsync,
        });
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }
        else
        {
            app.MapFallback("/openapi/{**path}", () => ApiProblems.Result(ApiProblems.NotFound(
                ApiErrorCodes.EndpointNotFound,
                "La route API demandée n'existe pas.")));
        }

        app.MapGitHealthApi();
        app.MapFallback("/api/{**path}", () => ApiProblems.Result(ApiProblems.NotFound(
            ApiErrorCodes.EndpointNotFound,
            "La route API demandée n’existe pas.")));
        app.MapFallbackToFile("index.html");
    }

    private static void ApplyLauncherConfiguration(
        ConfigurationManager configuration,
        LauncherOptions options)
    {
        ApplyRepositoryConfiguration(configuration, options.RepositoryPath);
        ApplyGitConfiguration(configuration, options.GitExecutablePath);
        ApplyDataConfiguration(configuration, options.DataDirectory);
    }

    private static void ApplyRepositoryConfiguration(
        ConfigurationManager configuration,
        string? repositoryPath)
    {
        if (!string.IsNullOrWhiteSpace(repositoryPath))
        {
            configuration["GitHealth:InitialRepositoryPath"] = Path.GetFullPath(
                repositoryPath);
        }
    }

    private static void ApplyGitConfiguration(
        ConfigurationManager configuration,
        string? gitExecutablePath)
    {
        if (!string.IsNullOrWhiteSpace(gitExecutablePath))
        {
            configuration[GitExecutableLocation.ConfigurationKey] = Path.GetFullPath(
                gitExecutablePath);
        }
    }

    private static void ApplyDataConfiguration(
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

    private static void RunDirect(
        WebApplication app,
        LauncherOptions options,
        bool useNativeLauncher)
    {
        try
        {
            if (useNativeLauncher)
            {
                DesktopLauncher.Run(app, options);
            }
            else
            {
                app.RunAsync().GetAwaiter().GetResult();
            }
        }
        catch (Exception exception)
        {
            ReportStartupFailure(exception, app, options);
            Environment.ExitCode = StartupFailureReporter.FailureExitCode;
        }
    }

    private static void ReportStartupFailure(
        Exception exception,
        WebApplication app,
        LauncherOptions options)
    {
        var databasePath = app.Configuration["Persistence:DatabasePath"] ?? "githealth.db";
        StartupFailureReporter.Write(
            Console.Error,
            StartupFailureReporter.Diagnose(exception, options.Port, databasePath));
    }

    private static bool IsContainer() => string.Equals(
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
        "true",
        StringComparison.OrdinalIgnoreCase);

    private static bool IsInvalidPath(Exception exception) => exception is ArgumentException
        or NotSupportedException
        or PathTooLongException;
}
