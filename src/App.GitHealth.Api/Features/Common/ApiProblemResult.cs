namespace App.GitHealth.Api.Features.Common;

internal sealed class ApiProblemResult(ApiFailure failure) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = failure.Code,
            ["traceId"] = httpContext.TraceIdentifier,
        };
        var result = Results.Problem(
            statusCode: failure.StatusCode,
            title: ApiProblems.TitleFor(failure.StatusCode),
            detail: failure.Detail,
            extensions: extensions);
        return result.ExecuteAsync(httpContext);
    }
}
