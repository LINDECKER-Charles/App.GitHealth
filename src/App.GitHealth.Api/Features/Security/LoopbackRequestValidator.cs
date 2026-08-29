using System.Net;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace App.GitHealth.Api.Features.Security;

internal sealed class LoopbackRequestValidator
{
    private static readonly StringComparer OriginComparer =
        StringComparer.OrdinalIgnoreCase;
    private readonly HashSet<string> _allowedOrigins;

    public LoopbackRequestValidator(IOptions<LocalSecurityOptions> options)
    {
        _allowedOrigins = options.Value.AllowedOrigins
            .Select(NormalizeOrigin)
            .ToHashSet(OriginComparer);
    }

    public static bool HasValidHost(HttpRequest request) =>
        IsLoopbackHost(request.Host.Host);

    public bool HasValidOrigin(HttpRequest request)
    {
        var values = request.Headers.Origin;
        if (StringValues.IsNullOrEmpty(values))
        {
            return true;
        }

        if (values.Count != 1 || !TryNormalizeOrigin(values[0], out var origin))
        {
            return false;
        }

        return OriginComparer.Equals(origin, RequestOrigin(request))
            || _allowedOrigins.Contains(origin);
    }

    public static bool HasValidFetchSite(HttpRequest request)
    {
        var values = request.Headers["Sec-Fetch-Site"];
        if (StringValues.IsNullOrEmpty(values))
        {
            return true;
        }

        return values.Count == 1
            && (string.Equals(values[0], "same-origin", StringComparison.Ordinal)
                || string.Equals(values[0], "none", StringComparison.Ordinal));
    }

    public static bool IsAllowedOrigin(string origin) =>
        TryNormalizeOrigin(origin, out _);

    private static string RequestOrigin(HttpRequest request)
    {
        var port = request.Host.Port ?? DefaultPort(request.Scheme);
        return NormalizeOrigin(new UriBuilder(
            request.Scheme,
            request.Host.Host,
            port).Uri);
    }

    private static string NormalizeOrigin(string origin)
    {
        if (!TryNormalizeOrigin(origin, out var normalized))
        {
            throw new FormatException(
                "L'origine configurée doit être une origine HTTP loopback.");
        }

        return normalized;
    }

    private static bool TryNormalizeOrigin(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !IsHttp(uri.Scheme)
            || !IsLoopbackHost(uri.Host)
            || uri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        normalized = NormalizeOrigin(uri);
        return true;
    }

    private static string NormalizeOrigin(Uri origin)
    {
        var port = origin.IsDefaultPort ? DefaultPort(origin.Scheme) : origin.Port;
        return new UriBuilder(origin.Scheme, origin.Host, port).Uri
            .GetLeftPart(UriPartial.Authority);
    }

    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));

    private static bool IsHttp(string scheme) =>
        string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static int DefaultPort(string scheme) =>
        string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80;
}
