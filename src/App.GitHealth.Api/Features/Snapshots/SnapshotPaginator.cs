using System.Globalization;
using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Features.Snapshots;

internal static class SnapshotPaginator
{
    private const int MaximumPageSize = 200;
    private static readonly string[] AllowedSorts = ["name", "ahead", "behind", "activity"];
    private static readonly string[] AllowedDirections = ["asc", "desc"];

    public static ApiOutcome<SnapshotPageData> Page(
        AnalysisRunEntity analysis,
        SnapshotQueryParameters query)
    {
        var validation = Validate(query);
        if (validation is not null)
        {
            return ApiOutcome<SnapshotPageData>.Failed(validation);
        }

        var branches = Filter(analysis.Branches, query).ToArray();
        var ordered = Order(branches, query).ToArray();
        var offset = FindOffset(analysis.Id, ordered, query);
        if (!offset.IsSuccess)
        {
            return ApiOutcome<SnapshotPageData>.Failed(offset.Failure!);
        }

        var page = ordered.Skip(offset.Value).Take(query.PageSize + 1).ToArray();
        var hasMore = page.Length > query.PageSize;
        var selected = page.Take(query.PageSize).ToArray();
        var next = hasMore ? EncodeNext(analysis.Id, selected[^1], query) : null;
        return ApiOutcome<SnapshotPageData>.Success(new SnapshotPageData(selected, next));
    }

    private static ApiFailure? Validate(SnapshotQueryParameters query)
    {
        if (query.PageSize is < 1 or > MaximumPageSize
            || !AllowedSorts.Contains(query.Sort, StringComparer.OrdinalIgnoreCase)
            || !AllowedDirections.Contains(query.Direction, StringComparer.OrdinalIgnoreCase))
        {
            return InvalidCursor("Les paramètres de pagination sont invalides.");
        }

        if (query.Relationship is not null
            && !Enum.TryParse<BranchRelationship>(query.Relationship, true, out _))
        {
            return InvalidCursor("Le filtre de relation Git est invalide.");
        }

        return null;
    }

    private static IEnumerable<BranchSnapshotEntity> Filter(
        IEnumerable<BranchSnapshotEntity> branches,
        SnapshotQueryParameters query)
    {
        var search = NormalizeSearch(query.Search);
        if (search is not null)
        {
            branches = branches.Where(branch => branch.ReferenceName.Contains(
                search,
                StringComparison.OrdinalIgnoreCase));
        }

        if (Enum.TryParse<BranchRelationship>(query.Relationship, true, out var relationship))
        {
            branches = branches.Where(branch => branch.Relationship == relationship);
        }

        return branches;
    }

    private static IOrderedEnumerable<BranchSnapshotEntity> Order(
        IEnumerable<BranchSnapshotEntity> branches,
        SnapshotQueryParameters query)
    {
        var descending = query.Direction.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var ordered = (query.Sort.ToLowerInvariant(), descending) switch
        {
            ("ahead", false) => branches.OrderBy(branch => branch.AheadCount),
            ("ahead", true) => branches.OrderByDescending(branch => branch.AheadCount),
            ("behind", false) => branches.OrderBy(branch => branch.BehindCount),
            ("behind", true) => branches.OrderByDescending(branch => branch.BehindCount),
            ("activity", false) => branches.OrderBy(branch => branch.LastActivityAtUtc),
            ("activity", true) => branches.OrderByDescending(branch => branch.LastActivityAtUtc),
            ("name", true) => branches.OrderByDescending(
                branch => branch.ReferenceName,
                StringComparer.Ordinal),
            _ => branches.OrderBy(branch => branch.ReferenceName, StringComparer.Ordinal),
        };
        return ordered.ThenBy(branch => branch.ReferenceName, StringComparer.Ordinal)
            .ThenBy(branch => branch.Id);
    }

    private static ApiOutcome<int> FindOffset(
        Guid analysisId,
        BranchSnapshotEntity[] ordered,
        SnapshotQueryParameters query)
    {
        if (query.Cursor is null)
        {
            return ApiOutcome<int>.Success(0);
        }

        if (!SnapshotCursor.TryDecode(query.Cursor, out var cursor)
            || !MatchesQuery(cursor!, analysisId, query))
        {
            return ApiOutcome<int>.Failed(InvalidCursor("Le curseur est invalide."));
        }

        var index = ordered.ToList().FindIndex(branch => branch.Id == cursor!.SnapshotId);
        if (index < 0 || !MatchesBranch(cursor!, ordered[index], query.Sort))
        {
            return ApiOutcome<int>.Failed(InvalidCursor("Le curseur n’est plus disponible."));
        }

        return ApiOutcome<int>.Success(index + 1);
    }

    private static bool MatchesQuery(
        SnapshotCursorData cursor,
        Guid analysisId,
        SnapshotQueryParameters query) =>
        cursor.AnalysisId == analysisId
        && cursor.Sort.Equals(query.Sort, StringComparison.OrdinalIgnoreCase)
        && cursor.Direction.Equals(query.Direction, StringComparison.OrdinalIgnoreCase)
        && cursor.Search == NormalizeSearch(query.Search)
        && cursor.Relationship == NormalizeRelationship(query.Relationship);

    private static bool MatchesBranch(
        SnapshotCursorData cursor,
        BranchSnapshotEntity branch,
        string sort) =>
        cursor.ReferenceName == branch.ReferenceName
        && cursor.SortValue == SortValue(branch, sort);

    private static string EncodeNext(
        Guid analysisId,
        BranchSnapshotEntity branch,
        SnapshotQueryParameters query)
    {
        return SnapshotCursor.Encode(new SnapshotCursorData
        {
            AnalysisId = analysisId,
            Sort = query.Sort.ToLowerInvariant(),
            Direction = query.Direction.ToLowerInvariant(),
            Search = NormalizeSearch(query.Search),
            Relationship = NormalizeRelationship(query.Relationship),
            SortValue = SortValue(branch, query.Sort),
            ReferenceName = branch.ReferenceName,
            SnapshotId = branch.Id,
        });
    }

    private static string SortValue(BranchSnapshotEntity branch, string sort) =>
        sort.ToLowerInvariant() switch
        {
            "ahead" => branch.AheadCount.ToString(CultureInfo.InvariantCulture),
            "behind" => branch.BehindCount.ToString(CultureInfo.InvariantCulture),
            "activity" => branch.LastActivityAtUtc?.ToUnixTimeMilliseconds()
                .ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            _ => branch.ReferenceName,
        };

    private static string? NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    private static string? NormalizeRelationship(string? relationship) =>
        Enum.TryParse<BranchRelationship>(relationship, true, out var value)
            ? value.ToString().ToLowerInvariant()
            : null;

    private static ApiFailure InvalidCursor(string detail) => ApiProblems.BadRequest(
        ApiErrorCodes.InvalidCursor,
        detail);
}
