using System.Net.Sockets;
using App.GitHealth.Api.Features.LocalApi.Surface;
using App.GitHealth.Api.Hosting;
using Microsoft.AspNetCore.HostFiltering;

namespace App.GitHealth.Api.Features.LocalApi;

/// <summary>
/// The second listener, and a host of its own rather than a route added to the interface's
/// pipeline. That prefix is the browser's: a mutation on it must carry the session cookie and
/// the anti-forgery token, and an orchestrator has neither. Serving both surfaces from one
/// pipeline would have meant relaxing that guard for the browser too, so this one answers to
/// something else entirely — a bearer token — on a port of its own. Both bind loopback.
/// </summary>
internal sealed class LocalApiHost(IServiceProvider application) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WebApplication? _listener;

    public LocalApiStatus Status { get; private set; } = LocalApiStatus.Closed;

    /// <summary>Port actually taken. Null whenever nothing is listening.</summary>
    public int? BoundPort { get; private set; }

    public string? FailureMessage { get; private set; }

    public async Task OpenAsync(int port, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await ShutDownAsync(cancellationToken);
            await BindAsync(port, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await ShutDownAsync(cancellationToken);
            Status = LocalApiStatus.Closed;
            FailureMessage = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The gate is deliberately left undisposed: the container disposes this singleton before
    /// the hosted service that closes the port gets its turn, and a disposed semaphore would
    /// turn an orderly shutdown into an exception. It holds no unmanaged handle — none is
    /// leaked by leaving it be.
    /// </summary>
    public ValueTask DisposeAsync() => new(CloseAsync(CancellationToken.None));

    private async Task BindAsync(int port, CancellationToken cancellationToken)
    {
        var listener = Build(port);
        try
        {
            await listener.StartAsync(cancellationToken);
        }
        catch (Exception exception) when (IsBindingFailure(exception))
        {
            await listener.DisposeAsync();
            Status = LocalApiStatus.Failed;
            BoundPort = null;
            FailureMessage = Describe(exception, port);
            return;
        }

        _listener = listener;
        Status = LocalApiStatus.Listening;
        BoundPort = port;
        FailureMessage = null;
    }

    private async Task ShutDownAsync(CancellationToken cancellationToken)
    {
        var listener = Interlocked.Exchange(ref _listener, null);
        if (listener is null)
        {
            return;
        }

        BoundPort = null;
        await listener.StopAsync(cancellationToken);
        await listener.DisposeAsync();
    }

    /// <summary>
    /// Production whatever the application runs as: a developer exception page has no place
    /// on a surface reached by a machine, and neither has an environment-dependent shape.
    /// </summary>
    private WebApplication Build(int port)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Production,
        });
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, string.Empty);

        // Host filtering ships with the slim builder and would answer a foreign host with a
        // bare 400 read out of appsettings.json. The rule belongs to this surface's own guard
        // instead, spelled the way the browser surface spells it and answered with the same
        // problem document, so a caller reads one shape of error and not two.
        builder.Services.Configure<HostFilteringOptions>(
            options => options.AllowedHosts = ["*"]);
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.AddServerHeader = false;
            server.Listen(LauncherOptions.ListenAddress, port);
        });

        var listener = builder.Build();
        listener.Use(LendApplicationServicesAsync);
        listener.Use(LocalApiAuthentication.InvokeAsync);
        listener.MapLocalApiEndpoints();
        return listener;
    }

    /// <summary>
    /// Hands each request the application's own services. This listener has a container of
    /// its own — it has to, it is a host — but nothing useful lives there: the repositories,
    /// the analysis queue and the database context all belong to the application, and a
    /// request answered out of a second copy of them would read a second, empty database.
    /// </summary>
    private async Task LendApplicationServicesAsync(HttpContext context, RequestDelegate next)
    {
        using var scope = application.CreateScope();
        context.RequestServices = scope.ServiceProvider;
        await next(context);
    }

    private static bool IsBindingFailure(Exception exception) =>
        exception is IOException or SocketException or InvalidOperationException;

    /// <summary>
    /// Kestrel wraps the real reason — the port is taken, the address is refused — inside a
    /// binding error of its own, so the message worth showing is the innermost one.
    /// </summary>
    private static string Describe(Exception exception, int port)
    {
        var cause = exception;
        while (cause.InnerException is not null)
        {
            cause = cause.InnerException;
        }

        return $"Port {port} could not be opened: {cause.Message}";
    }
}
