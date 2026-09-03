using System.Globalization;
using System.Text;

namespace App.GitHealth.Core.Assistant;

/// <summary>
/// Describes a capture to the person deciding whether an agent may read it. This is not the
/// text an agent is given — nothing is: the agent queries GitHealth and receives one answer
/// at a time. What is written here is the whole of what those answers can contain, so that
/// permission is granted against something seen rather than against a promise.
/// </summary>
public static class BriefingWriter
{
    private const string NotAvailable = "—";
    private const string NoPattern = "none";

    private static readonly string[] Columns =
    [
        "branch", "ahead", "behind", "relationship", "last commit", "topology",
        "activity", "verdict", "reason", "flags", "author",
    ];

    private static readonly string[] Questions =
    [
        $"`{AssistantPrompt.CaptureTool}` — the four facts above, and the policy in force.",
        $"`{AssistantPrompt.ListTool}` — the rows below, filtered and read a page at a time.",
        $"`{AssistantPrompt.BranchTool}` — one of the rows below, named exactly.",
        $"`{AssistantPrompt.CountTool}` — how many rows fall in each verdict, topology,"
            + " activity or author.",
    ];

    public static string Write(AnalysisBriefing briefing)
    {
        ArgumentNullException.ThrowIfNull(briefing);
        var builder = new StringBuilder();
        WriteHeader(builder, briefing);
        WritePolicy(builder, briefing.Policy);
        WriteQuestions(builder);
        WriteBranches(builder, briefing);
        return builder.ToString();
    }

    private static void WriteHeader(StringBuilder builder, AnalysisBriefing briefing)
    {
        builder.AppendLine("# What the agent can query");
        builder.AppendLine();
        builder.AppendLine(
            "GitHealth serves this capture over a local bridge, one question at a time. It is"
            + " never handed over as a document, and nothing below leaves this machine until"
            + " the agent asks for it. This is the whole of what it can ever get back.");
        builder.AppendLine();
        builder.AppendLine(Line("Repository", briefing.RepositoryName));
        builder.AppendLine(Line("Baseline compared against", briefing.Baseline));
        builder.AppendLine(Line("Captured at", Timestamp(briefing.CapturedAt)));
        builder.AppendLine(Line(
            "Branches measured",
            briefing.MeasuredBranchCount.ToString(CultureInfo.InvariantCulture)));
        builder.AppendLine();
    }

    private static void WritePolicy(StringBuilder builder, BriefingPolicy policy)
    {
        builder.AppendLine("## Policy in force");
        builder.AppendLine();
        builder.AppendLine(Line("Active while the last commit is under", Days(policy.ActiveUntilDays)));
        builder.AppendLine(Line("Inactive once the last commit is over", Days(policy.InactiveAfterDays)));
        builder.AppendLine(Line("Protected patterns", Patterns(policy.ProtectedPatterns)));
        builder.AppendLine(Line("Excluded patterns", Patterns(policy.ExcludedPatterns)));
        builder.AppendLine();
    }

    /// <summary>
    /// The four questions the bridge answers. Naming them is the point: it is what tells a
    /// reader that the agent pulls what it needs rather than being pushed all of it.
    /// </summary>
    private static void WriteQuestions(StringBuilder builder)
    {
        builder.AppendLine("## What it can ask for");
        builder.AppendLine();
        foreach (var question in Questions)
        {
            builder.AppendLine("- " + question);
        }

        builder.AppendLine();
    }

    private static void WriteBranches(StringBuilder builder, AnalysisBriefing briefing)
    {
        builder.AppendLine("## The branches it can read");
        builder.AppendLine();
        builder.AppendLine("| " + string.Join(" | ", Columns) + " |");
        builder.AppendLine("|" + string.Concat(Columns.Select(_ => "---|")));
        foreach (var branch in briefing.Branches)
        {
            builder.AppendLine(Row(branch));
        }

        WriteOmission(builder, briefing.OmittedBranchCount);
    }

    private static void WriteOmission(StringBuilder builder, int omitted)
    {
        if (omitted == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine(
            $"> {omitted.ToString(CultureInfo.InvariantCulture)} further branches were measured "
            + "but are out of reach of the bridge: the agent cannot read them, and any count it "
            + "gives is a count over the rows above.");
    }

    private static string Row(BriefingBranch branch)
    {
        string[] cells =
        [
            Cell(branch.ReferenceName),
            branch.AheadCount.ToString(CultureInfo.InvariantCulture),
            branch.BehindCount.ToString(CultureInfo.InvariantCulture),
            Cell(branch.Relationship),
            Day(branch.LastActivityAt),
            Cell(branch.Topology),
            Cell(branch.Activity),
            Cell(branch.Recommendation),
            Cell(branch.Reason),
            Flags(branch),
            Cell(branch.TipAuthor ?? NotAvailable),
        ];
        return $"| {string.Join(" | ", cells)} |";
    }

    private static string Flags(BriefingBranch branch)
    {
        var flags = new List<string>(capacity: 2);
        if (branch.IsProtected)
        {
            flags.Add("protected");
        }

        if (branch.IsExcluded)
        {
            flags.Add("excluded");
        }

        return flags.Count == 0 ? NotAvailable : string.Join(" ", flags);
    }

    private static string Line(string label, string value) => $"- **{label}**: {value}";

    private static string Days(int days) =>
        $"{days.ToString(CultureInfo.InvariantCulture)} days";

    private static string Patterns(IReadOnlyList<string> patterns) => patterns.Count == 0
        ? NoPattern
        : string.Join(", ", patterns.Select(pattern => $"`{pattern}`"));

    private static string Timestamp(DateTimeOffset moment) =>
        moment.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string Day(DateTimeOffset? moment) => moment is null
        ? NotAvailable
        : moment.Value.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A pipe inside a cell would split the row and shift every column after it.</summary>
    private static string Cell(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ").Trim();
}
