namespace App.GitHealth.Api.Features.Security;

internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    // base-uri 'self' rather than 'none': the Angular application declares
    // <base href="/">. Blocked, the tag let the relative URLs of index.html resolve
    // from the current route, and every reloaded deep address served an empty page.
    // 'self' still forbids the only threat targeted: a <base> to another origin.
    private const string ContentSecurityPolicy =
        "default-src 'self'; base-uri 'self'; object-src 'none'; "
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
