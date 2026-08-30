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

    // HttpOnly = false est délibéré : XSRF-TOKEN porte la moitié publique du
    // double-submit antiforgery, que le client Angular relit dans document.cookie
    // (withXsrfConfiguration) pour la réémettre en en-tête X-XSRF-TOKEN. La moitié
    // secrète reste le cookie GitHealth.Antiforgery, lui HttpOnly. Le passer en
    // HttpOnly désactiverait la protection CSRF sans rien protéger : aucun secret de
    // session ne transite ici. CodeQL cs/web/cookie-httponly-not-set le signale malgré
    // tout, faute de distinguer cookie de session et request token — faux positif.
    private static CookieOptions RequestTokenCookieOptions(bool isSecure) => new()
    {
        HttpOnly = false,
        IsEssential = true,
        Path = "/",
        SameSite = SameSiteMode.Strict,
        Secure = isSecure,
    };
}
