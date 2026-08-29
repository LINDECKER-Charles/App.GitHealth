namespace App.GitHealth.Api.Hosting;

internal sealed record LauncherParseResult
{
    private LauncherParseResult()
    {
    }

    public LauncherOptions? Options { get; private init; }

    public string? ErrorMessage { get; private init; }

    public bool IsSuccess => Options is not null;

    public static LauncherParseResult Success(LauncherOptions options) => new()
    {
        Options = options,
    };

    public static LauncherParseResult Failure(string errorMessage) => new()
    {
        ErrorMessage = errorMessage,
    };
}
