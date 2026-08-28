using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Persistence;

internal sealed class PersistenceOptionsValidator : IValidateOptions<PersistenceOptions>
{
    public ValidateOptionsResult Validate(string? name, PersistenceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DatabasePath))
        {
            return ValidateOptionsResult.Fail("Le chemin de la base SQLite est obligatoire.");
        }

        if (options.WriteTimeoutSeconds is < PersistenceOptions.MinimumWriteTimeoutSeconds
            or > PersistenceOptions.MaximumWriteTimeoutSeconds)
        {
            return ValidateOptionsResult.Fail(
                "Le délai d’écriture doit être compris entre 1 et 60 s.");
        }

        return options.RetentionDays is <= 0
            ? ValidateOptionsResult.Fail("La rétention doit être positive ou désactivée.")
            : ValidateOptionsResult.Success;
    }
}
