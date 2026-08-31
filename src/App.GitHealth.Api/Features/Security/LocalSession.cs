using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;

namespace App.GitHealth.Api.Features.Security;

internal static class LocalSession
{
    public const string AntiforgeryCookieName = "GitHealth.Antiforgery";
    public const string AntiforgeryHeaderName = "X-XSRF-TOKEN";
    public const string RequestTokenCookieName = "XSRF-TOKEN";
    public const string SessionCookieName = "GitHealth.Session";
    private const string SessionNonceKey = "GitHealth.LocalSession.Nonce";
    private const int SessionNonceSize = 32;

    public static bool Exists(ISession session) =>
        session.TryGetValue(SessionNonceKey, out var nonce)
        && nonce.Length == SessionNonceSize;

    public static void Initialize(HttpContext context, IAntiforgery antiforgery)
    {
        if (!Exists(context.Session))
        {
            context.Session.Set(SessionNonceKey, RandomNumberGenerator.GetBytes(SessionNonceSize));
        }

        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append(
            RequestTokenCookieName,
            tokens.RequestToken!,
            RequestTokenCookieOptions(context.Request.IsHttps));
    }

    // HttpOnly = false is deliberate: XSRF-TOKEN carries the public half of the
    // antiforgery double-submit, which the Angular client reads back from
    // document.cookie (withXsrfConfiguration) to re-emit it in the X-XSRF-TOKEN
    // header. The secret half stays in the GitHealth.Antiforgery cookie, which is
    // HttpOnly. Turning this one HttpOnly would disable the CSRF protection while
    // protecting nothing: no session secret travels here. CodeQL
    // cs/web/cookie-httponly-not-set flags it all the same, for lack of telling a
    // session cookie from a request token — false positive.
    private static CookieOptions RequestTokenCookieOptions(bool isSecure) => new()
    {
        HttpOnly = false,
        IsEssential = true,
        Path = "/",
        SameSite = SameSiteMode.Strict,
        Secure = isSecure,
    };
}
