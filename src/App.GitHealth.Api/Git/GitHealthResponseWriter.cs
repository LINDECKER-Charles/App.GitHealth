using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace App.GitHealth.Api.Git;

internal static class GitHealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    message = entry.Value.Description,
                }),
        };
        return JsonSerializer.SerializeAsync(context.Response.Body, response);
    }
}
