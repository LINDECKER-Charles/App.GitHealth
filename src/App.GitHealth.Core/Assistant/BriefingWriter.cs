using System.Globalization;
using System.Text;

namespace App.GitHealth.Core.Assistant;

/// <summary>
/// Renders a briefing as Markdown. The result is shown to the user, character for
/// character, before it is allowed to leave the machine — so it is written to be read by a
/// human first and a model second.
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

    public static string Write(AnalysisBriefing briefing)
    {
        ArgumentNullException.ThrowIfNull(briefing);
        var builder = new StringBuilder();
        WriteHeader(builder, briefing);
        WritePolicy(builder, briefing.Policy);
        WriteLegend(builder);
        WriteBranches(builder, briefing);
        return builder.ToString();
    }

    private static void WriteHeader(StringBuilder builder, AnalysisBriefing briefing)
    {
        builder.AppendLine("# Branch capture");
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

    private static void WriteLegend(StringBuilder builder)
    {
        builder.AppendLine("## How to read a row");
        builder.AppendLine();
        builder.AppendLine("- `ahead` — commits the branch carries that the baseline does not.");
        builder.AppendLine("- `behind` — commits the baseline carries that the branch does not.");
        builder.AppendLine("- `verdict` and `reason` — what GitHealth concluded, and why. They are");
        builder.AppendLine("  a starting point, not a constraint: disagree with them when the facts");
        builder.AppendLine("  in the row support it, and say so explicitly.");
        builder.AppendLine("- `flags` — `protected` shields a branch from any cleanup advice;");
        builder.AppendLine("  `excluded` means the policy leaves it out of the reading entirely.");
        builder.AppendLine();
    }

    private static void WriteBranches(StringBuilder builder, AnalysisBriefing briefing)
    {
        builder.AppendLine("## Branches");
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
            + "but left out of this table. Any count you give is a count over the rows above, "
            + "not over the repository — say so.");
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
