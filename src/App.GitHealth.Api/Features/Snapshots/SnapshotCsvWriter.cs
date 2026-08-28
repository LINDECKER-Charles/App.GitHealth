using System.Globalization;
using System.Text;

namespace App.GitHealth.Api.Features.Snapshots;

internal static class SnapshotCsvWriter
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@'];
    private static readonly UTF8Encoding Utf8WithBom = new(
        encoderShouldEmitUTF8Identifier: true);

    public static byte[] Write(IEnumerable<BranchSnapshotResponse> snapshots)
    {
        var csv = new StringBuilder();
        AppendRow(csv,
        [
            "referenceName", "commitId", "aheadCount", "behindCount", "relationship",
            "lastActivityAtUtc", "tipAuthor", "topology", "activity", "recommendation",
            "reason", "isProtected", "isExcluded",
        ]);
        foreach (var snapshot in snapshots)
        {
            AppendRow(csv, Map(snapshot));
        }

        var content = Utf8WithBom.GetBytes(csv.ToString());
        return [.. Utf8WithBom.GetPreamble(), .. content];
    }

    private static string?[] Map(BranchSnapshotResponse snapshot) =>
    [
        snapshot.ReferenceName,
        snapshot.CommitId,
        snapshot.AheadCount.ToString(CultureInfo.InvariantCulture),
        snapshot.BehindCount.ToString(CultureInfo.InvariantCulture),
        snapshot.Relationship,
        snapshot.LastActivityAtUtc?.ToString("O", CultureInfo.InvariantCulture),
        snapshot.TipAuthor,
        snapshot.Topology,
        snapshot.Activity,
        snapshot.Recommendation,
        snapshot.Reason,
        snapshot.IsProtected.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
        snapshot.IsExcluded.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
    ];

    private static void AppendRow(StringBuilder csv, string?[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            if (index > 0)
            {
                csv.Append(',');
            }

            csv.Append('"');
            csv.Append(Neutralize(values[index]).Replace("\"", "\"\"", StringComparison.Ordinal));
            csv.Append('"');
        }

        csv.Append("\r\n");
    }

    private static string Neutralize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var firstMeaningful = value.AsSpan().TrimStart();
        return firstMeaningful.Length > 0 && FormulaPrefixes.Contains(firstMeaningful[0])
            ? $"'{value}"
            : value;
    }
}
