namespace App.GitHealth.Core.Analysis;

public enum RepositoryErrorCode
{
    GitUnavailable,
    PathNotFound,
    PathNotAllowed,
    NotARepository,
    InvalidReference,
    MalformedOutput,
    TimedOut,
    ProcessFailed,
}
