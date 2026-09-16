using Microsoft.Data.Sqlite;

namespace App.GitHealth.Api.Tests.Persistence;

/// <summary>
/// Releases the pooled handles of one database, so the directory holding it can be deleted.
/// </summary>
/// <remarks>
/// Never <see cref="SqliteConnection.ClearAllPools"/> from a test: it is process-wide, while
/// these tests run side by side. Clearing every pool disposes the handle another test is in the
/// middle of using — which surfaces as an <c>ObjectDisposedException</c> on
/// <c>SQLitePCL.sqlite3</c> — or hands it a fresh connection that never received the pragmas
/// the migration set, such as <c>busy_timeout</c>.
/// </remarks>
internal static class SqlitePool
{
    public static void Clear(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        SqliteConnection.ClearPool(connection);
    }
}
