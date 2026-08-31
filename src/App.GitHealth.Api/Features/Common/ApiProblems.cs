using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Api.Features.Common;

internal static class ApiProblems
{
    public static IResult Result(ApiFailure failure)
        => new ApiProblemResult(failure);

    public static ApiFailure BadRequest(string code, string detail) =>
        Create(StatusCodes.Status400BadRequest, code, detail);

    public static ApiFailure NotFound(string code, string detail) =>
        Create(StatusCodes.Status404NotFound, code, detail);

    public static ApiFailure Forbidden(string code, string detail) =>
        Create(StatusCodes.Status403Forbidden, code, detail);

    public static ApiFailure Conflict(string code, string detail) =>
        Create(StatusCodes.Status409Conflict, code, detail);

    public static ApiFailure Unavailable(string code, string detail) =>
        Create(StatusCodes.Status503ServiceUnavailable, code, detail);

    public static ApiFailure FromRepository(RepositoryError error)
    {
        return error.Code switch
        {
            RepositoryErrorCode.PathNotFound =>
                BadRequest(ApiErrorCodes.InvalidPath, error.Message),
            RepositoryErrorCode.PathNotAllowed =>
                BadRequest(ApiErrorCodes.PathNotAllowed, error.Message),
            RepositoryErrorCode.NotARepository =>
                BadRequest(ApiErrorCodes.InvalidRepository, error.Message),
            RepositoryErrorCode.InvalidReference =>
                BadRequest(ApiErrorCodes.InvalidReference, error.Message),
            RepositoryErrorCode.GitUnavailable =>
                Unavailable(ApiErrorCodes.ScannerUnavailable, error.Message),
            _ => BadRequest(ApiErrorCodes.InvalidRepository, error.Message),
        };
    }

    private static ApiFailure Create(int statusCode, string code, string detail) => new()
    {
        StatusCode = statusCode,
        Code = code,
        Detail = detail,
    };

    internal static string TitleFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad request",
        StatusCodes.Status403Forbidden => "Access denied",
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status503ServiceUnavailable => "Service unavailable",
        _ => "Error",
    };
}
