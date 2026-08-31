using System.Globalization;
using App.GitHealth.Api.Git.Models;
using App.GitHealth.Api.Git.Process;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Git.Parsing;

internal static class GitOutputParser
{
    private const int ReferenceFieldCount = 5;

    public static IReadOnlyDictionary<string, CapturedReference> ParseReferences(string output)
    {
        var fields = output.Split('\0');
        var result = new Dictionary<string, CapturedReference>(StringComparer.Ordinal);
        var index = SkipEmptyFields(fields);

        while (index + ReferenceFieldCount <= fields.Length)
        {
            var reference = ParseReference(fields, ref index);
            if (reference is null)
            {
                continue;
            }

            result.Add(reference.Reference.FullName, reference);
        }

        EnsureComplete(fields, index, "The Git reference list is incomplete.");
        return result;
    }

    public static IReadOnlyDictionary<string, (CommitId Commit, int Ahead, int Behind)>
        ParseAheadBehind(string output)
    {
        var fields = output.Split('\0');
        var result = new Dictionary<string, (CommitId, int, int)>(StringComparer.Ordinal);
        var index = 0;

        while (index + 3 <= fields.Length)
        {
            var name = TrimRecordSeparator(fields[index++]);
            var objectId = fields[index++];
            var counts = fields[index++];
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var (ahead, behind) = ParseCounts(counts);
            result[name] = (new CommitId(objectId), ahead, behind);
        }

        EnsureComplete(fields, index, "The Git ahead/behind output is incomplete.");
        return result;
    }

    private static CapturedReference? ParseReference(string[] fields, ref int index)
    {
        var name = TrimRecordSeparator(fields[index++]);
        var objectId = fields[index++];
        var symbolicTarget = NullIfEmpty(fields[index++]);
        var timestamp = fields[index++];
        var author = NullIfEmpty(fields[index++]);
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        try
        {
            var tip = new BranchTip(new CommitId(objectId), ParseTimestamp(timestamp), author);
            return new CapturedReference(new GitRef(name), tip, symbolicTarget);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw Malformed("The Git reference list is malformed.", exception);
        }
    }

    private static (int Ahead, int Behind) ParseCounts(string counts)
    {
        var values = counts.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != 2
            || !int.TryParse(values[0], CultureInfo.InvariantCulture, out var ahead)
            || !int.TryParse(values[1], CultureInfo.InvariantCulture, out var behind))
        {
            throw Malformed("The Git ahead/behind counters are malformed.");
        }

        return (ahead, behind);
    }

    public static (int Ahead, int Behind) ParseRevListCounts(string output)
    {
        var values = output.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != 2
            || !int.TryParse(values[0], CultureInfo.InvariantCulture, out var behind)
            || !int.TryParse(values[1], CultureInfo.InvariantCulture, out var ahead))
        {
            throw Malformed("The Git rev-list counters are malformed.");
        }

        return (ahead, behind);
    }

    public static IReadOnlyList<Contributor> ParseContributors(string output)
    {
        var fields = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var counts = new Dictionary<(string Name, string Email), int>();
        for (var index = 0; index < fields.Length; index += 2)
        {
            if (index + 1 >= fields.Length)
            {
                throw Malformed("The Git contributor list is incomplete.");
            }

            var name = TrimRecordSeparator(fields[index]);
            var email = fields[index + 1].Trim('\r', '\n');
            var key = (name, email);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key.Name, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Email, StringComparer.Ordinal)
            .Select(pair => new Contributor(pair.Key.Name, pair.Key.Email, pair.Value))
            .ToArray();
    }

    private static DateTimeOffset? ParseTimestamp(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (!long.TryParse(value, CultureInfo.InvariantCulture, out var unixTime))
        {
            throw new FormatException("Invalid Git timestamp.");
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixTime);
    }

    private static string TrimRecordSeparator(string value) => value.Trim('\r', '\n');

    private static int SkipEmptyFields(string[] fields)
    {
        var index = 0;
        while (index < fields.Length && string.IsNullOrWhiteSpace(fields[index]))
        {
            index++;
        }

        return index;
    }

    private static void EnsureComplete(string[] fields, int index, string message)
    {
        if (fields.Skip(index).Any(field => !string.IsNullOrWhiteSpace(field)))
        {
            throw Malformed(message);
        }
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrEmpty(value) ? null : value;

    private static GitProcessException Malformed(string message, Exception? inner = null) =>
        new(RepositoryErrorCode.MalformedOutput, message, inner);
}
