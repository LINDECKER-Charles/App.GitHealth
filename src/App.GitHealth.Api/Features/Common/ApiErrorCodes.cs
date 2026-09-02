namespace App.GitHealth.Api.Features.Common;

internal static class ApiErrorCodes
{
    public const string AnalysisNotFound = "analysis.not_found";
    public const string AnalysisNotAvailable = "analysis.no_successful_result";
    public const string AnalysisRunning = "analysis.running";
    public const string AssistantAgentUnavailable = "assistant.agent_unavailable";
    public const string AssistantBusy = "assistant.busy";
    public const string AssistantDisabled = "assistant.disabled";
    public const string AssistantQuestionRequired = "assistant.question_required";
    public const string AssistantRunFailed = "assistant.run_failed";
    public const string AssistantRunNotFound = "assistant.run_not_found";
    public const string AssistantTimedOut = "assistant.timed_out";
    public const string DatabaseBusy = "database.busy";
    public const string DirectoryInaccessible = "runtime.directory_inaccessible";
    public const string DirectoryNotAllowed = "runtime.directory_not_allowed";
    public const string DirectoryNotFound = "runtime.directory_not_found";
    public const string EndpointNotFound = "endpoint.not_found";
    public const string CrossSiteRequest = "security.cross_site_request";
    public const string InvalidAntiforgeryToken = "security.invalid_antiforgery_token";
    public const string InvalidHost = "security.invalid_host";
    public const string InvalidDirectory = "runtime.invalid_directory";
    public const string InvalidCursor = "pagination.invalid_cursor";
    public const string InvalidPage = "pagination.invalid_page";
    public const string InvalidPath = "repository.invalid_path";
    public const string InvalidReference = "repository.invalid_reference";
    public const string InvalidRepository = "repository.invalid";
    public const string InvalidRequest = "validation.invalid_request";
    public const string PathNotAllowed = "repository.path_not_allowed";
    public const string ProjectAlreadyExists = "project.already_exists";
    public const string ProjectBusy = "project.busy";
    public const string RepositoryIdentityMismatch = "repository.identity_mismatch";
    public const string ProjectNotFound = "project.not_found";
    public const string QueueFull = "analysis.queue_full";
    public const string ScannerUnavailable = "git.unavailable";
    public const string SnapshotNotFound = "snapshot.not_found";
}
