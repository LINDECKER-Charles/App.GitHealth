using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Persistence;

internal sealed class PersistenceOptionsValidator : IValidateOptions<PersistenceOptions>
{
    public ValidateOptionsResult Validate(string? name, PersistenceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DatabasePath))
        {
            return ValidateOptionsResult.Fail("The SQLite database path is required.");
        }

        if (options.WriteTimeoutSeconds is < PersistenceOptions.MinimumWriteTimeoutSeconds
            or > PersistenceOptions.MaximumWriteTimeoutSeconds)
        {
            return ValidateOptionsResult.Fail(
                "The write timeout must be between 1 and 60 s.");
        }

        return options.RetentionDays is <= 0
            ? ValidateOptionsResult.Fail("The retention must be positive or disabled.")
            : ValidateOptionsResult.Success;
    }
}
