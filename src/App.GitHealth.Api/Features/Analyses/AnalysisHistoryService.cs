using System.Text.Json;
using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;

namespace App.GitHealth.Api.Features.Analyses;

internal sealed class AnalysisHistoryService(
    IAnalysisRepository analyses,
    IProjectRepository projects)
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    public async Task<ApiOutcome<AnalysisHistoryPageResponse>> GetAsync(
        Guid projectId,
        AnalysisHistoryQueryParameters query,
        CancellationToken cancellationToken)
    {
        if (!IsValid(query))
        {
            return ApiOutcome<AnalysisHistoryPageResponse>.Failed(ApiProblems.BadRequest(
                ApiErrorCodes.InvalidPage,
                "Les paramètres de pagination sont invalides."));
        }

        if (await projects.GetAsync(projectId, cancellationToken) is null)
        {
            return ApiOutcome<AnalysisHistoryPageResponse>.Failed(ApiProblems.NotFound(
                ApiErrorCodes.ProjectNotFound,
                "Le projet demandé n’existe pas."));
        }

        var pageNumber = Page(query);
        var pageSize = PageSize(query);
        var skip = checked((pageNumber - 1) * pageSize);
        var page = await analyses.GetHistoryAsync(
            projectId,
            new AnalysisHistoryRange(skip, pageSize),
            cancellationToken);
        return ApiOutcome<AnalysisHistoryPageResponse>.Success(Map(page, query));
    }

    private static bool IsValid(AnalysisHistoryQueryParameters query) =>
        Page(query) is > 0 and <= int.MaxValue / MaximumPageSize
        && PageSize(query) is > 0 and <= MaximumPageSize;

    private static AnalysisHistoryPageResponse Map(
        AnalysisHistoryPage page,
        AnalysisHistoryQueryParameters query) => new()
        {
            Items = page.Items.Select(Map).ToArray(),
            Page = Page(query),
            PageSize = PageSize(query),
            TotalCount = page.TotalCount,
        };

    private static int Page(AnalysisHistoryQueryParameters query) =>
        query.Page ?? DefaultPage;

    private static int PageSize(AnalysisHistoryQueryParameters query) =>
        query.PageSize ?? DefaultPageSize;

    private static AnalysisHistoryItemResponse Map(AnalysisHistoryRecord analysis) => new()
    {
        AnalysisId = analysis.AnalysisId,
        Status = analysis.Status.ToString(),
        StartedAtUtc = analysis.StartedAtUtc,
        CompletedAtUtc = analysis.CompletedAtUtc,
        CapturedAtUtc = analysis.CapturedAtUtc,
        ReferenceName = analysis.ReferenceName,
        ReferenceCommit = analysis.ReferenceCommit,
        BranchNamespace = analysis.BranchNamespace,
        ActiveUntilDays = analysis.ActiveUntilDays,
        InactiveAfterDays = analysis.InactiveAfterDays,
        ExcludedPatterns = ReadPatterns(analysis.ExcludedPatternsJson),
        ProtectedPatterns = ReadPatterns(analysis.ProtectedPatternsJson),
        GitVersion = analysis.GitVersion,
        BranchCount = analysis.BranchCount,
        FailureCode = analysis.FailureCode,
        FailureMessage = analysis.FailureMessage,
    };

    private static string[] ReadPatterns(string json) =>
        JsonSerializer.Deserialize<string[]>(json) ?? [];
}
