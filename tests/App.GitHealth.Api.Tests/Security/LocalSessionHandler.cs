using System.Net.Http.Headers;
using App.GitHealth.Api.Features.Security;

namespace App.GitHealth.Api.Tests.Security;

internal sealed class LocalSessionHandler : DelegatingHandler
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private string? _cookieHeader;
    private string? _requestToken;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (IsMutation(request.Method))
        {
            await EnsureSessionAsync(request.RequestUri!, cancellationToken);
        }

        if (_cookieHeader is not null)
        {
            request.Headers.TryAddWithoutValidation("Cookie", _cookieHeader);
        }

        if (IsMutation(request.Method) && _requestToken is not null)
        {
            request.Headers.TryAddWithoutValidation(
                LocalSession.AntiforgeryHeaderName,
                _requestToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _initializationLock.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task EnsureSessionAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        if (_requestToken is not null)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_requestToken is null)
            {
                await InitializeSessionAsync(requestUri, cancellationToken);
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task InitializeSessionAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        using var navigation = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(requestUri, "/"));
        navigation.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        navigation.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        using var response = await base.SendAsync(navigation, cancellationToken);
        var cookies = ParseCookies(response.Headers.GetValues("Set-Cookie"));
        _cookieHeader = string.Join("; ", cookies.Values);
        _requestToken = Uri.UnescapeDataString(
            cookies[LocalSession.RequestTokenCookieName].Split('=', 2)[1]);
    }

    private static Dictionary<string, string> ParseCookies(IEnumerable<string> values)
    {
        return values
            .Select(value => value.Split(';', 2)[0])
            .Select(value => (Pair: value, Separator: value.IndexOf('=')))
            .Where(cookie => cookie.Separator > 0)
            .ToDictionary(
                cookie => cookie.Pair[..cookie.Separator],
                cookie => cookie.Pair,
                StringComparer.Ordinal);
    }

    private static bool IsMutation(HttpMethod method) =>
        method != HttpMethod.Get
        && method != HttpMethod.Head
        && method != HttpMethod.Options;
}
