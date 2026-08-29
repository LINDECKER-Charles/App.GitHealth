namespace App.GitHealth.Api.Features.Security;

internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; base-uri 'none'; object-src 'none'; "
        + "frame-ancestors 'none'; form-action 'self'; "
        + "script-src 'self'; style-src 'self' 'unsafe-inline'; "
        + "img-src 'self' data:; font-src 'self'; connect-src 'self'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(AddHeaders, context.Response);
        return next(context);
    }

    private static Task AddHeaders(object state)
    {
        var headers = ((HttpResponse)state).Headers;
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] =
            "camera=(), geolocation=(), microphone=(), payment=(), usb=()";
        return Task.CompletedTask;
    }
}
