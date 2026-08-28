using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Git.Models;

internal sealed record GitComparison(
    GitRepositoryContext Context,
    CommitId Reference,
    CommitId Branch);
