namespace App.GitHealth.Api.Persistence;

internal static class UtcDate
{
    public static void Require(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("La date doit être en UTC.", parameterName);
        }
    }
}
