using System.Globalization;
using App.GitHealth.Api.Persistence.Services;
using App.GitHealth.Core.Common;

namespace App.GitHealth.Api.Features.Exports;

internal static class DatabaseExportEndpoints
{
    public static IEndpointRouteBuilder MapDatabaseExportEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/exports/database", ExportAsync).WithTags("Exports");
        return endpoints;
    }

    private static async Task ExportAsync(
        HttpContext context,
        IDatabaseBackupService backupService,
        IClock clock)
    {
        var timestamp = clock.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        context.Response.ContentType = "application/vnd.sqlite3";
        context.Response.Headers.ContentDisposition =
            $"attachment; filename=githealth-backup-{timestamp}.db";
        await backupService.ExportAsync(context.Response.Body, context.RequestAborted);
    }
}
