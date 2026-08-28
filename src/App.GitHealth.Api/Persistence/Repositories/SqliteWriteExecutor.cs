using App.GitHealth.Api.Persistence.Models;
using Microsoft.Data.Sqlite;

namespace App.GitHealth.Api.Persistence.Repositories;

internal static class SqliteWriteExecutor
{
    private const int BusyErrorCode = 5;
    private const int LockedErrorCode = 6;

    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception exception) when (FindBusyError(exception) is not null)
        {
            throw new PersistenceWriteException(
                PersistenceErrorCode.DatabaseBusy,
                "La base SQLite est occupée au-delà du délai d’écriture configuré.",
                exception);
        }
    }

    private static SqliteException? FindBusyError(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException sqlite
                && sqlite.SqliteErrorCode is BusyErrorCode or LockedErrorCode)
            {
                return sqlite;
            }
        }

        return null;
    }
}
