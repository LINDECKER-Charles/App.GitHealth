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
                "Le délai Git doit être compris entre 1 et 120 secondes.");
        }

        if (options.MaximumOutputBytes is < GitScannerOptions.MinimumOutputBytes
            or > GitScannerOptions.MaximumOutputBytesLimit)
        {
            return ValidateOptionsResult.Fail(
                "La sortie Git doit être limitée entre 1 Kio et 16 Mio.");
        }

        return options.MaximumParallelCommands
                is < GitScannerOptions.MinimumParallelCommands
                or > GitScannerOptions.MaximumParallelCommandsLimit
            ? ValidateOptionsResult.Fail(
                "La concurrence Git doit être comprise entre 1 et 8.")
            : ValidateOptionsResult.Success;
    }
}
