namespace App.GitHealth.Benchmarks.Fixtures;

internal sealed record GitCommandRequest(
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    string? StandardInput = null);
