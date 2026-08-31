using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Git;

internal sealed class GitScannerOptionsValidator : IValidateOptions<GitScannerOptions>
{
    public ValidateOptionsResult Validate(string? name, GitScannerOptions options)
    {
        var seconds = options.CommandTimeout.TotalSeconds;
        if (seconds is < GitScannerOptions.MinimumCommandTimeoutSeconds
            or > GitScannerOptions.MaximumCommandTimeoutSeconds)
        {
            return ValidateOptionsResult.Fail(
                "The Git timeout must be between 1 and 120 seconds.");
        }

        if (options.MaximumOutputBytes is < GitScannerOptions.MinimumOutputBytes
            or > GitScannerOptions.MaximumOutputBytesLimit)
        {
            return ValidateOptionsResult.Fail(
                "The Git output must be limited to between 1 KiB and 16 MiB.");
        }

        return options.MaximumParallelCommands
                is < GitScannerOptions.MinimumParallelCommands
                or > GitScannerOptions.MaximumParallelCommandsLimit
            ? ValidateOptionsResult.Fail(
                "The Git concurrency must be between 1 and 8.")
            : ValidateOptionsResult.Success;
    }
}
