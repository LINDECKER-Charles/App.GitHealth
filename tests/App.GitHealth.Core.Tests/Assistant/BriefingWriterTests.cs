using App.GitHealth.Core.Assistant;

namespace App.GitHealth.Core.Tests.Assistant;

public sealed class BriefingWriterTests
{
    private static readonly DateTimeOffset CapturedAt =
        new(2026, 9, 2, 10, 34, 0, TimeSpan.Zero);

    [Fact]
    public void HeaderCarriesTheRepositoryTheBaselineAndTheCapture()
    {
        var text = BriefingWriter.Write(Briefing(Branch("refs/heads/feat/login")));

        Assert.Contains("**Repository**: Storefront", text, StringComparison.Ordinal);
        Assert.Contains("**Baseline compared against**: main", text, StringComparison.Ordinal);
        Assert.Contains("2026-09-02 10:34 UTC", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PolicyIsStatedSoAVerdictCanBeRead()
    {
        var text = BriefingWriter.Write(Briefing(Branch("refs/heads/feat/login")));

        Assert.Contains("under**: 30 days", text, StringComparison.Ordinal);
        Assert.Contains("over**: 90 days", text, StringComparison.Ordinal);
        Assert.Contains("`main`", text, StringComparison.Ordinal);
        Assert.Contains("**Excluded patterns**: none", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EachBranchBecomesOneRowCarryingItsVerdict()
    {
        var branch = Branch("refs/heads/feat/login") with
        {
            AheadCount = 12,
            BehindCount = 3,
            Recommendation = "cleanup",
            Reason = "no commit in 140 days",
            TipAuthor = "Alex Rivera",
        };

        var row = RowFor(BriefingWriter.Write(Briefing(branch)), "refs/heads/feat/login");

        Assert.Contains("| 12 | 3 |", row, StringComparison.Ordinal);
        Assert.Contains("cleanup", row, StringComparison.Ordinal);
        Assert.Contains("no commit in 140 days", row, StringComparison.Ordinal);
        Assert.Contains("Alex Rivera", row, StringComparison.Ordinal);
    }

    [Fact]
    public void FlagsReadAsWordsRatherThanAsBooleans()
    {
        var branch = Branch("refs/heads/main") with { IsProtected = true, IsExcluded = true };

        var row = RowFor(BriefingWriter.Write(Briefing(branch)), "refs/heads/main");

        Assert.Contains("protected excluded", row, StringComparison.Ordinal);
    }

    /// <summary>A pipe would split the row and shift every column after it.</summary>
    [Fact]
    public void PipesInsideAValueAreEscapedRatherThanBreakingTheTable()
    {
        var branch = Branch("refs/heads/feat/a|b");

        var text = BriefingWriter.Write(Briefing(branch));

        var row = RowFor(text, "refs/heads/feat/a\\|b");
        Assert.Equal(ColumnCount(text), ColumnCount(row));
    }

    [Fact]
    public void AnUnknownLastCommitIsSaidRatherThanDated()
    {
        var branch = Branch("refs/heads/orphan") with { LastActivityAt = null };

        var row = RowFor(BriefingWriter.Write(Briefing(branch)), "refs/heads/orphan");

        Assert.Contains("—", row, StringComparison.Ordinal);
    }

    /// <summary>
    /// A truncated table that says so beats one that reads as complete: a count over 40 rows
    /// is not a count over the repository, and both readers have to be told which it is.
    /// </summary>
    [Fact]
    public void TruncationIsAnnouncedWithTheNumberOfBranchesLeftOut()
    {
        var briefing = Briefing(Branch("refs/heads/feat/login")) with
        {
            OmittedBranchCount = 40,
        };

        var text = BriefingWriter.Write(briefing);

        Assert.Contains("40 further branches", text, StringComparison.Ordinal);
        Assert.Contains("out of reach of the bridge", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// This text used to be the prompt itself, pasted into the agent's standard input. It is
    /// now read by a person deciding whether to allow a repository to be queried at all, so
    /// it has to say that the agent asks for what it needs rather than being handed all of it.
    /// </summary>
    [Fact]
    public void TheCaptureIsDescribedAsSomethingQueriedRatherThanSent()
    {
        var text = BriefingWriter.Write(Briefing(Branch("refs/heads/feat/login")));

        Assert.Contains("# What the agent can query", text, StringComparison.Ordinal);
        Assert.Contains("never handed over as a document", text, StringComparison.Ordinal);
        Assert.DoesNotContain("How to read a row", text, StringComparison.Ordinal);
    }

    /// <summary>Naming the four questions is what shows the reader that the agent pulls.</summary>
    [Fact]
    public void TheFourQuestionsTheBridgeAnswersAreNamed()
    {
        var text = BriefingWriter.Write(Briefing(Branch("refs/heads/feat/login")));

        Assert.Contains(AssistantPrompt.CaptureTool, text, StringComparison.Ordinal);
        Assert.Contains(AssistantPrompt.ListTool, text, StringComparison.Ordinal);
        Assert.Contains(AssistantPrompt.BranchTool, text, StringComparison.Ordinal);
        Assert.Contains(AssistantPrompt.CountTool, text, StringComparison.Ordinal);
    }

    [Fact]
    public void ACompleteTableCarriesNoTruncationNotice()
    {
        var text = BriefingWriter.Write(Briefing(Branch("refs/heads/feat/login")));

        Assert.DoesNotContain("further branches", text, StringComparison.Ordinal);
    }

    private static string RowFor(string text, string reference) => text
        .Split(Environment.NewLine)
        .Single(line => line.StartsWith($"| {reference} |", StringComparison.Ordinal));

    /// <summary>
    /// Counts cells the way a Markdown reader does — on the separator, not on the character,
    /// so an escaped pipe inside a value stays part of its cell. Applied to the whole text it
    /// returns the header's width, which is what every row has to match.
    /// </summary>
    private static int ColumnCount(string rowOrText)
    {
        var row = rowOrText.Split(Environment.NewLine)
            .First(line => line.StartsWith("| ", StringComparison.Ordinal));
        return row.Trim().Trim('|').Split(" | ").Length;
    }

    private static AnalysisBriefing Briefing(params BriefingBranch[] branches) => new()
    {
        RepositoryName = "Storefront",
        Baseline = "main",
        CapturedAt = CapturedAt,
        Policy = new BriefingPolicy
        {
            ActiveUntilDays = 30,
            InactiveAfterDays = 90,
            ProtectedPatterns = ["main"],
            ExcludedPatterns = [],
        },
        Branches = branches,
    };

    private static BriefingBranch Branch(string reference) => new()
    {
        ReferenceName = reference,
        AheadCount = 1,
        BehindCount = 0,
        Relationship = "ahead",
        Topology = "diverged",
        Activity = "active",
        Recommendation = "keep",
        Reason = "still moving",
        LastActivityAt = CapturedAt.AddDays(-2),
    };
}
