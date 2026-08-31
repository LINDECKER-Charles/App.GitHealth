using App.GitHealth.Api.Features.Common;
using Microsoft.AspNetCore.Antiforgery;

namespace App.GitHealth.Api.Features.Security;

internal sealed class LocalRequestSecurityMiddleware(
    RequestDelegate next,
    LoopbackRequestValidator validator)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        var requestFailure = FailureFor(context.Request);
        if (requestFailure is not null)
        {
            await RejectAsync(context, requestFailure);
            return;
        }

        if (ShouldInitializeSession(context.Request))
        {
            LocalSession.Initialize(context, antiforgery);
        }

        if (IsApiMutation(context.Request)
            && !await HasValidMutationSessionAsync(context, antiforgery))
        {
            await RejectAsync(context, AntiforgeryFailure());
            return;
        }

        await next(context);
    }

    private ApiFailure? FailureFor(HttpRequest request)
    {
        if (!LoopbackRequestValidator.HasValidHost(request))
        {
            return ApiProblems.BadRequest(
                ApiErrorCodes.InvalidHost,
                "The HTTP host must be loopback.");
        }

        return IsApiRequest(request) && !HasTrustedBrowserContext(request)
            ? ApiProblems.Forbidden(
                ApiErrorCodes.CrossSiteRequest,
                "The request comes from an unauthorised origin.")
            : null;
    }

    private static ApiFailure AntiforgeryFailure() => ApiProblems.Forbidden(
        ApiErrorCodes.InvalidAntiforgeryToken,
        "A local navigation is required before any modification.");

    private bool HasTrustedBrowserContext(HttpRequest request) =>
        validator.HasValidOrigin(request)
        && LoopbackRequestValidator.HasValidFetchSite(request);

    private static async Task<bool> HasValidMutationSessionAsync(
        HttpContext context,
        IAntiforgery antiforgery)
    {
        if (!LocalSession.Exists(context.Session))
        {
            return false;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static bool IsApiRequest(HttpRequest request) =>
        request.Path.StartsWithSegments("/api");

    private static bool IsApiMutation(HttpRequest request) =>
        IsApiRequest(request)
        && !HttpMethods.IsGet(request.Method)
        && !HttpMethods.IsHead(request.Method)
        && !HttpMethods.IsOptions(request.Method);

    private static bool IsHtmlNavigation(HttpRequest request) =>
        HttpMethods.IsGet(request.Method)
        && !IsApiRequest(request)
        && !request.Path.StartsWithSegments("/health")
        && !request.Path.StartsWithSegments("/openapi")
        && (request.Headers.Accept.Any(value => value?.Contains(
                "text/html",
                StringComparison.OrdinalIgnoreCase) == true)
            || string.Equals(
                request.Headers["Sec-Fetch-Mode"],
                "navigate",
                StringComparison.Ordinal));

    private static bool ShouldInitializeSession(HttpRequest request) =>
        IsHtmlNavigation(request)
        || (HttpMethods.IsGet(request.Method)
            && request.Path.Equals(LocalSessionEndpoints.Path));

    private static Task RejectAsync(HttpContext context, ApiFailure failure) =>
        ApiProblems.Result(failure).ExecuteAsync(context);
}
