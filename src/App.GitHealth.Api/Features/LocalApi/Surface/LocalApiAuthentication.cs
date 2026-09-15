using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Features.Security;
using Microsoft.Extensions.Primitives;

namespace App.GitHealth.Api.Features.LocalApi.Surface;

/// <summary>
/// The whole authorisation of this surface, and deliberately the whole of it: a bearer token
/// weighed in constant time against the fingerprint on disk. No session, no cookie, no origin
/// — an orchestrator is not a browser and carries none of them. The loopback host check stays
/// on top, because a name that resolves to 127.0.0.1 is how a web page would try to reach a
/// listener it has no business reaching.
/// </summary>
internal static class LocalApiAuthentication
{
    private const string BearerScheme = "Bearer";
    private const string ChallengePrefix = BearerScheme + " ";

    public static async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var failure = Authorize(context);
        if (failure is null)
        {
            await next(context);
            return;
        }

        context.Response.Headers.WWWAuthenticate = BearerScheme;
        await ApiProblems.Result(failure).ExecuteAsync(context);
    }

    private static ApiFailure? Authorize(HttpContext context)
    {
        if (!LoopbackRequestValidator.HasValidHost(context.Request))
        {
            return ApiProblems.BadRequest(
                ApiErrorCodes.InvalidHost,
                "The HTTP host must be loopback.");
        }

        var settings = context.RequestServices
            .GetRequiredService<LocalApiSettingsStore>()
            .Read();
        return LocalApiToken.Matches(ReadToken(context.Request), settings.TokenFingerprint)
            ? null
            : ApiProblems.Unauthorized(
                ApiErrorCodes.LocalApiTokenRejected,
                "This request carries no valid local API token.");
    }

    private static string? ReadToken(HttpRequest request)
    {
        var values = request.Headers.Authorization;
        if (StringValues.IsNullOrEmpty(values) || values.Count != 1)
        {
            return null;
        }

        var header = values[0];
        return header is not null
            && header.StartsWith(ChallengePrefix, StringComparison.OrdinalIgnoreCase)
            ? header[ChallengePrefix.Length..].Trim()
            : null;
    }
}
