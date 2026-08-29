using Microsoft.AspNetCore.Antiforgery;

namespace App.GitHealth.Api.Features.Security;

internal static class LocalRequestSecurityExtensions
{
    private static readonly TimeSpan SessionIdleTimeout = TimeSpan.FromHours(12);

    public static IServiceCollection AddLocalRequestSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<LocalSecurityOptions>()
            .Bind(configuration.GetSection(LocalSecurityOptions.SectionName))
            .Validate(HasValidOrigins, "Les origines autorisées doivent être loopback.")
            .ValidateOnStart();
        services.AddDistributedMemoryCache();
        services.AddSession(ConfigureSession);
        services.AddAntiforgery(ConfigureAntiforgery);
        services.AddSingleton<LoopbackRequestValidator>();
        return services;
    }

    public static IApplicationBuilder UseLocalRequestSecurity(this IApplicationBuilder app)
    {
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseSession();
        app.UseMiddleware<LocalRequestSecurityMiddleware>();
        return app;
    }

    private static bool HasValidOrigins(LocalSecurityOptions options) =>
        options.AllowedOrigins.All(LoopbackRequestValidator.IsAllowedOrigin);

    private static void ConfigureSession(SessionOptions options)
    {
        options.Cookie.Name = LocalSession.SessionCookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.IdleTimeout = SessionIdleTimeout;
    }

    private static void ConfigureAntiforgery(AntiforgeryOptions options)
    {
        options.Cookie.Name = LocalSession.AntiforgeryCookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.HeaderName = LocalSession.AntiforgeryHeaderName;
    }
}
