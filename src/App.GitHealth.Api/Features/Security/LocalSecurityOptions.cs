namespace App.GitHealth.Api.Features.Security;

internal sealed class LocalSecurityOptions
{
    public const string SectionName = "LocalSecurity";

    public string[] AllowedOrigins { get; init; } = [];
}
