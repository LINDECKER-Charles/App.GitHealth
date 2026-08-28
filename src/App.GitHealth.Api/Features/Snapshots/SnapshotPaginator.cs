using System.Globalization;
using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Features.Snapshots;

internal static class SnapshotPaginator
{
    private const int DefaultPageSize = 50;
    private const int MaximumPageSize = 200;
    private static readonly string[] AllowedSorts = ["name", "ahead", "behind", "activity"];
    private static readonly string[] AllowedDirections = ["asc", "desc"];

    public static ApiOutcome<SnapshotPageData> Page(
        Guid analysisId,
        IReadOnlyList<ClassifiedSnapshot> branches,
        SnapshotQueryParameters query)
    {
        if (query.PageSize is < 1 or > MaximumPageSize)
        {
            return ApiOutcome<SnapshotPageData>.Failed(
                InvalidCursor("La taille de page est invalide."));
        }

        var selection = Select(branches, query);
        if (!selection.IsSuccess)
        {
            return ApiOutcome<SnapshotPageData>.Failed(selection.Failure!);
        }

        return CreatePage(analysisId, selection.Value!, query);
    }

    public static ApiOutcome<ClassifiedSnapshot[]> Select(
        IEnumerable<ClassifiedSnapshot> branches,
        SnapshotFilterParameters query)
    {
        var validation = ValidateFilters(query);
        if (validation is not null)
        {
            return ApiOutcome<ClassifiedSnapshot[]>.Failed(validation);
        }

        var filtered = Filter(branches, query);
        return ApiOutcome<ClassifiedSnapshot[]>.Success(Order(filtered, query).ToArray());
    }

    private static ApiOutcome<SnapshotPageData> CreatePage(
        Guid analysisId,
        ClassifiedSnapshot[] ordered,
        SnapshotQueryParameters query)
    {
        var offset = FindOffset(analysisId, ordered, query);
        if (!offset.IsSuccess)
        {
            return ApiOutcome<SnapshotPageData>.Failed(offset.Failure!);
        }

        var pageSize = PageSize(query);
        var page = ordered.Skip(offset.Value).Take(pageSize + 1).ToArray();
        var hasMore = page.Length > pageSize;
        var selected = page.Take(pageSize).ToArray();
        var next = hasMore ? EncodeNext(analysisId, selected[^1], query) : null;
        return ApiOutcome<SnapshotPageData>.Success(new SnapshotPageData(selected, next));
    }

    private static ApiFailure? ValidateFilters(SnapshotFilterParameters query)
    {
        if (!AllowedSorts.Contains(Sort(query), StringComparer.Ordinal)
            || !AllowedDirections.Contains(Direction(query), StringComparer.Ordinal))
        {
            return InvalidCursor("Les paramètres de tri sont invalides.");
        }

        return ValidateEnum<BranchRelationship>(query.Relationship, "relation Git")
            ?? ValidateEnum<BranchTopology>(query.Topology, "topologie")
            ?? ValidateEnum<ActivityStatus>(query.Activity, "activité")
            ?? ValidateEnum<RecommendationKind>(query.Recommendation, "recommandation");
    }

    private static ApiFailure? ValidateEnum<T>(string? value, string filterName)
        where T : struct, Enum
    {
        return value is not null && !Enum.TryParse<T>(value, true, out _)
            ? InvalidCursor($"Le filtre de {filterName} est invalide.")
            : null;
    }

    private static IEnumerable<ClassifiedSnapshot> Filter(
        IEnumerable<ClassifiedSnapshot> branches,
        SnapshotFilterParameters query)
    {
        var search = NormalizeSearch(query.Search);
        if (search is not null)
        {
            branches = branches.Where(item => item.Branch.ReferenceName.Contains(
                search,
                StringComparison.OrdinalIgnoreCase));
        }

        branches = FilterEnum(
            branches,
            query.Relationship,
            item => item.Branch.Relationship);
        branches = FilterEnum(branches, query.Topology, item => item.Comparison.Topology);
        branches = FilterEnum(branches, query.Activity, item => item.Comparison.Activity);
        branches = FilterEnum(
            branches,
            query.Recommendation,
            item => item.Comparison.Recommendation);
        return FilterFlags(branches, query);
    }

    private static IEnumerable<ClassifiedSnapshot> FilterEnum<T>(
        IEnumerable<ClassifiedSnapshot> branches,
        string? requested,
        Func<ClassifiedSnapshot, T> select)
        where T : struct, Enum
    {
        return Enum.TryParse<T>(requested, true, out var value)
            ? branches.Where(branch => EqualityComparer<T>.Default.Equals(select(branch), value))
            : branches;
    }

    private static IEnumerable<ClassifiedSnapshot> FilterFlags(
        IEnumerable<ClassifiedSnapshot> branches,
        SnapshotFilterParameters query)
    {
        if (query.IsProtected.HasValue)
        {
            branches = branches.Where(item =>
                item.Comparison.IsProtected == query.IsProtected.Value);
        }

        if (query.IsExcluded.HasValue)
        {
            branches = branches.Where(item =>
                item.Comparison.IsExcluded == query.IsExcluded.Value);
        }

        return branches;
    }

    private static IOrderedEnumerable<ClassifiedSnapshot> Order(
        IEnumerable<ClassifiedSnapshot> branches,
        SnapshotFilterParameters query)
    {
        var descending = Direction(query) == "desc";
        var ordered = (Sort(query), descending) switch
        {
            ("ahead", false) => branches.OrderBy(item => item.Branch.AheadCount),
            ("ahead", true) => branches.OrderByDescending(item => item.Branch.AheadCount),
            ("behind", false) => branches.OrderBy(item => item.Branch.BehindCount),
            ("behind", true) => branches.OrderByDescending(item => item.Branch.BehindCount),
            ("activity", false) => branches.OrderBy(item => item.Branch.LastActivityAtUtc),
            ("activity", true) => branches.OrderByDescending(
                item => item.Branch.LastActivityAtUtc),
            ("name", true) => branches.OrderByDescending(
                item => item.Branch.ReferenceName,
                StringComparer.Ordinal),
            _ => branches.OrderBy(
                item => item.Branch.ReferenceName,
                StringComparer.Ordinal),
        };
        return ordered.ThenBy(item => item.Branch.ReferenceName, StringComparer.Ordinal)
            .ThenBy(item => item.Branch.Id);
    }

    private static ApiOutcome<int> FindOffset(
        Guid analysisId,
        ClassifiedSnapshot[] ordered,
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

        var index = Array.FindIndex(ordered, item => item.Branch.Id == cursor!.SnapshotId);
        if (index < 0 || !MatchesBranch(cursor!, ordered[index], Sort(query)))
        {
            return ApiOutcome<int>.Failed(
                InvalidCursor("Le curseur n’est plus disponible."));
        }

        return ApiOutcome<int>.Success(index + 1);
    }

    private static bool MatchesQuery(
        SnapshotCursorData cursor,
        Guid analysisId,
        SnapshotQueryParameters query) =>
        cursor.AnalysisId == analysisId
        && cursor.Sort == Sort(query)
        && cursor.Direction == Direction(query)
        && cursor.Search == NormalizeSearch(query.Search)
        && cursor.Relationship == NormalizeEnum<BranchRelationship>(query.Relationship)
        && cursor.Topology == NormalizeEnum<BranchTopology>(query.Topology)
        && cursor.Activity == NormalizeEnum<ActivityStatus>(query.Activity)
        && cursor.Recommendation == NormalizeEnum<RecommendationKind>(query.Recommendation)
        && cursor.IsProtected == query.IsProtected
        && cursor.IsExcluded == query.IsExcluded;

    private static bool MatchesBranch(
        SnapshotCursorData cursor,
        ClassifiedSnapshot item,
        string sort) =>
        cursor.ReferenceName == item.Branch.ReferenceName
        && cursor.SortValue == SortValue(item.Branch, sort);

    private static string EncodeNext(
        Guid analysisId,
        ClassifiedSnapshot item,
        SnapshotQueryParameters query)
    {
        return SnapshotCursor.Encode(new SnapshotCursorData
        {
            AnalysisId = analysisId,
            Sort = Sort(query),
            Direction = Direction(query),
            Search = NormalizeSearch(query.Search),
            Relationship = NormalizeEnum<BranchRelationship>(query.Relationship),
            Topology = NormalizeEnum<BranchTopology>(query.Topology),
            Activity = NormalizeEnum<ActivityStatus>(query.Activity),
            Recommendation = NormalizeEnum<RecommendationKind>(query.Recommendation),
            IsProtected = query.IsProtected,
            IsExcluded = query.IsExcluded,
            SortValue = SortValue(item.Branch, Sort(query)),
            ReferenceName = item.Branch.ReferenceName,
            SnapshotId = item.Branch.Id,
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

    private static string Sort(SnapshotFilterParameters query) =>
        string.IsNullOrWhiteSpace(query.Sort) ? "name" : query.Sort.ToLowerInvariant();

    private static string Direction(SnapshotFilterParameters query) =>
        string.IsNullOrWhiteSpace(query.Direction)
            ? "asc"
            : query.Direction.ToLowerInvariant();

    private static int PageSize(SnapshotQueryParameters query) =>
        query.PageSize ?? DefaultPageSize;

    private static string? NormalizeEnum<T>(string? value)
        where T : struct, Enum => Enum.TryParse<T>(value, true, out var parsed)
            ? parsed.ToString().ToLowerInvariant()
            : null;

    private static ApiFailure InvalidCursor(string detail) => ApiProblems.BadRequest(
        ApiErrorCodes.InvalidCursor,
        detail);
}
