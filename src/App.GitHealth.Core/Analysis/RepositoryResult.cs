namespace App.GitHealth.Core.Analysis;

public sealed class RepositoryResult<T>
{
    internal RepositoryResult(T? value, RepositoryError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public RepositoryError? Error { get; }

    public bool TryGetValue([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? value)
    {
        value = Value;
        return IsSuccess;
    }

}
