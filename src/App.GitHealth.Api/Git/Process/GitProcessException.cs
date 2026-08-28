using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Api.Git.Process;

internal sealed class GitProcessException : Exception
{
    public GitProcessException(RepositoryErrorCode code, string message, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
    }

    public RepositoryErrorCode Code { get; }
}
