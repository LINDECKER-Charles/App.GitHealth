using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using App.GitHealth.Core.Assistant;

namespace App.GitHealth.Api.Features.Assistant.Mcp;

/// <summary>
/// Answers one tool call from one capture. Everything it can reach is already in memory and
/// already measured: there is no database read here, no Git call, and no way to name a
/// project — the capture arrives with the session, so a call cannot widen what it sees.
/// </summary>
internal static class AssistantMcpToolRunner
{
    private const string NotAvailable = "—";
    private const string NoAuthor = "(no author recorded)";

    private static readonly string[] Legend =
    [
        "ahead — commits the branch carries that the baseline does not.",
        "behind — commits the baseline carries that the branch does not.",
        "verdict and reason — what GitHealth concluded, and why. A starting point, not a"
            + " constraint: disagree when the branch's own facts support it, and say so.",
        "flags — protected shields a branch from cleanup advice; excluded means the policy"
            + " leaves it out of the reading entirely.",
    ];

    public static AssistantMcpToolResult Run(
        AnalysisBriefing capture,
        string name,
        JsonElement arguments)
    {
        ArgumentNullException.ThrowIfNull(capture);
        return name switch
        {
            AssistantMcpTools.GetCapture => Ok(Describe(capture)),
            AssistantMcpTools.ListBranches => Ok(List(capture, ReadQuery(arguments))),
            AssistantMcpTools.GetBranch => Find(capture, Text(arguments, "branch")),
            AssistantMcpTools.CountBranches => Group(capture, Text(arguments, "groupBy")),
            _ => AssistantMcpToolResult.Error($"There is no \"{name}\" tool."),
        };
    }

    private static JsonObject Describe(AnalysisBriefing capture) => new JsonObject
    {
        ["repository"] = capture.RepositoryName,
        ["baseline"] = capture.Baseline,
        ["capturedAtUtc"] = capture.CapturedAt.ToUniversalTime().ToString("O", Invariant),
        ["branchesMeasured"] = capture.MeasuredBranchCount,
        ["branchesReadable"] = capture.Branches.Count,
        ["branchesOmitted"] = capture.OmittedBranchCount,
        ["policy"] = Describe(capture.Policy),
        ["howToReadABranch"] = Strings(Legend),
    };

    private static JsonObject Describe(BriefingPolicy policy) => new JsonObject
    {
        ["activeUntilDays"] = policy.ActiveUntilDays,
        ["inactiveAfterDays"] = policy.InactiveAfterDays,
        ["protectedPatterns"] = Strings(policy.ProtectedPatterns),
        ["excludedPatterns"] = Strings(policy.ExcludedPatterns),
    };

    private static JsonObject List(AnalysisBriefing capture, BranchQuery query)
    {
        var page = query.Apply(capture.Branches);
        return new JsonObject
        {
            ["matched"] = query.Count(capture.Branches),
            ["skip"] = Math.Max(0, query.Skip),
            ["returned"] = page.Count,
            ["branchesOmittedFromCapture"] = capture.OmittedBranchCount,
            ["branches"] = new JsonArray([.. page.Select(Describe)]),
        };
    }

    private static AssistantMcpToolResult Find(AnalysisBriefing capture, string? wanted)
    {
        if (string.IsNullOrWhiteSpace(wanted))
        {
            return AssistantMcpToolResult.Error("A branch name is required.");
        }

        var branch = capture.Branches.FirstOrDefault(candidate => string.Equals(
            candidate.ReferenceName,
            wanted,
            StringComparison.OrdinalIgnoreCase));
        return branch is null
            ? AssistantMcpToolResult.Error(
                $"\"{wanted}\" is not in this capture. Use list_branches to see what is.")
            : Ok(Describe(branch));
    }

    private static AssistantMcpToolResult Group(AnalysisBriefing capture, string? field)
    {
        var select = Selector(field);
        if (select is null)
        {
            return AssistantMcpToolResult.Error(
                "groupBy must be verdict, topology, activity or author.");
        }

        var groups = capture.Branches
            .GroupBy(select)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new JsonObject
            {
                ["value"] = group.Key,
                ["count"] = group.Count(),
            } as JsonNode);
        return Ok(new JsonObject
        {
            ["groupBy"] = field,
            ["over"] = capture.Branches.Count,
            ["groups"] = new JsonArray([.. groups]),
        });
    }

    private static Func<BriefingBranch, string>? Selector(string? field) => field switch
    {
        "verdict" => branch => branch.Recommendation,
        "topology" => branch => branch.Topology,
        "activity" => branch => branch.Activity,
        "author" => branch => branch.TipAuthor ?? NoAuthor,
        _ => null,
    };

    private static JsonObject Describe(BriefingBranch branch) => new JsonObject
    {
        ["branch"] = branch.ReferenceName,
        ["ahead"] = branch.AheadCount,
        ["behind"] = branch.BehindCount,
        ["relationship"] = branch.Relationship,
        ["lastCommit"] = Day(branch.LastActivityAt),
        ["topology"] = branch.Topology,
        ["activity"] = branch.Activity,
        ["verdict"] = branch.Recommendation,
        ["reason"] = branch.Reason,
        ["isProtected"] = branch.IsProtected,
        ["isExcluded"] = branch.IsExcluded,
        ["author"] = branch.TipAuthor,
    };

    private static BranchQuery ReadQuery(JsonElement arguments) => new()
    {
        Verdict = Text(arguments, "verdict"),
        Topology = Text(arguments, "topology"),
        Activity = Text(arguments, "activity"),
        Author = Text(arguments, "author"),
        NameContains = Text(arguments, "nameContains"),
        IsProtected = Flag(arguments, "isProtected"),
        IsExcluded = Flag(arguments, "isExcluded"),
        Skip = Number(arguments, "skip") ?? 0,
        Take = Number(arguments, "take") ?? BranchQuery.DefaultTake,
    };

    private static AssistantMcpToolResult Ok(JsonNode payload) =>
        AssistantMcpToolResult.Success(payload);

    private static string? Text(JsonElement arguments, string name) =>
        Read(arguments, name) is { ValueKind: JsonValueKind.String } value
            ? value.GetString()
            : null;

    private static bool? Flag(JsonElement arguments, string name) => Read(arguments, name)
        is { ValueKind: JsonValueKind.True or JsonValueKind.False } value
            ? value.GetBoolean()
            : null;

    private static int? Number(JsonElement arguments, string name) =>
        Read(arguments, name) is { ValueKind: JsonValueKind.Number } value
        && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static JsonElement? Read(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object
        && arguments.TryGetProperty(name, out var value)
            ? value
            : null;

    private static JsonArray Strings(IEnumerable<string> values) =>
        new([.. values.Select(value => JsonValue.Create(value) as JsonNode)]);

    private static string Day(DateTimeOffset? moment) => moment is null
        ? NotAvailable
        : moment.Value.ToUniversalTime().ToString("yyyy-MM-dd", Invariant);

    private static CultureInfo Invariant => CultureInfo.InvariantCulture;
}
