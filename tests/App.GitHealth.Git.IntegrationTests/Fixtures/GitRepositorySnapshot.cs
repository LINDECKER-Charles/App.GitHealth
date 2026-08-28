namespace App.GitHealth.Git.IntegrationTests.Fixtures;

internal sealed record GitRepositorySnapshot(string References, string Status, string IndexHash);
