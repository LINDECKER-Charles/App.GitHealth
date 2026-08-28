namespace App.GitHealth.Api.Features.Common;

internal static class ApiErrorCodes
{
    public const string AnalysisNotFound = "analysis.not_found";
    public const string AnalysisNotAvailable = "analysis.no_successful_result";
    public const string DatabaseBusy = "database.busy";
    public const string InvalidCursor = "pagination.invalid_cursor";
    public const string InvalidPath = "repository.invalid_path";
    public const string InvalidReference = "repository.invalid_reference";
    public const string InvalidRepository = "repository.invalid";
    public const string InvalidRequest = "validation.invalid_request";
    public const string PathNotAllowed = "repository.path_not_allowed";
    public const string ProjectAlreadyExists = "project.already_exists";
    public const string ProjectNotFound = "project.not_found";
    public const string QueueFull = "analysis.queue_full";
    public const string ScannerUnavailable = "git.unavailable";
    public const string SnapshotNotFound = "snapshot.not_found";
}
