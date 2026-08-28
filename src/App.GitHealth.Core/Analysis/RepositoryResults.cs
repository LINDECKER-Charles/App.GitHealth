namespace App.GitHealth.Core.Analysis;

public static class RepositoryResults
{
    public static RepositoryResult<T> Success<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new RepositoryResult<T>(value, null);
    }

    public static RepositoryResult<T> Failure<T>(RepositoryError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new RepositoryResult<T>(default, error);
    }
}
