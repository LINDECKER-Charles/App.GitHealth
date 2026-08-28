using App.GitHealth.Api.Persistence.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace App.GitHealth.Api.Features.Common;

internal sealed partial class PersistenceExceptionHandler(
    ILogger<PersistenceExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not PersistenceWriteException persistenceException)
        {
            return false;
        }

        LogWriteFailure(logger, exception);
        var failure = ApiProblems.Unavailable(
            ApiErrorCodes.DatabaseBusy,
            persistenceException.Message);
        await ApiProblems.Result(failure).ExecuteAsync(httpContext);
        return true;
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Une écriture SQLite a échoué.")]
    private static partial void LogWriteFailure(ILogger logger, Exception exception);
}
