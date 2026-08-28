namespace App.GitHealth.Api.Persistence.Models;

internal sealed class PersistenceWriteException : Exception
{
    public PersistenceWriteException(
        PersistenceErrorCode code,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public PersistenceErrorCode Code { get; }
}
