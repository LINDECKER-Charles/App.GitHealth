namespace App.GitHealth.Core.Analysis;

public sealed record RepositoryError(RepositoryErrorCode Code, string Message);
