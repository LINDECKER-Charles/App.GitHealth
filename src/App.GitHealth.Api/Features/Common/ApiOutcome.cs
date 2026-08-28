namespace App.GitHealth.Api.Features.Common;

internal sealed class ApiOutcome<T>
{
    private ApiOutcome(T? value, ApiFailure? failure)
    {
        Value = value;
        Failure = failure;
    }

    public T? Value { get; }

    public ApiFailure? Failure { get; }

    public bool IsSuccess => Failure is null;

    public static ApiOutcome<T> Success(T value) => new(value, null);

    public static ApiOutcome<T> Failed(ApiFailure failure) => new(default, failure);
}

internal sealed record ApiFailure
{
    public required int StatusCode { get; init; }

    public required string Code { get; init; }

    public required string Detail { get; init; }
}
