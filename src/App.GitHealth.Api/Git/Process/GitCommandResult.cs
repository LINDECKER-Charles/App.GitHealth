namespace App.GitHealth.Api.Git.Process;

internal sealed record GitCommandResult(int ExitCode, string StandardOutput, string StandardError);
