using App.GitHealth.Core.Assistant;

namespace App.GitHealth.Core.Tests.Assistant;

public sealed class AssistantPromptTests
{
    [Fact]
    public void ThePromptCarriesTheCaptureAndThenTheQuestion()
    {
        var prompt = AssistantPrompt.Compose(Briefing(), "Which branches can I clean up?");

        var capture = prompt.IndexOf("# Branch capture", StringComparison.Ordinal);
        var question = prompt.IndexOf("## Question", StringComparison.Ordinal);
        Assert.True(capture >= 0);
        Assert.True(question > capture);
        Assert.Contains("Which branches can I clean up?", prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// The framing is the only thing keeping the agent inside the capture. If it ever stops
    /// being stated, the agent starts inventing branches instead of reading them.
    /// </summary>
    [Fact]
    public void ThePromptTellsTheAgentItHasNoAccessToTheRepository()
    {
        var prompt = AssistantPrompt.Compose(Briefing(), "Anything to clean?");

        Assert.Contains("no access to the repository", prompt, StringComparison.Ordinal);
        Assert.Contains("Never invent one.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePromptForbidsActingOnTheRepository()
    {
        var prompt = AssistantPrompt.Compose(Briefing(), "Anything to clean?");

        Assert.Contains(
            "deletes, merges and pushes nothing, and neither do you",
            prompt,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("  spaced  ", "spaced")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void AQuestionIsTrimmedBeforeItIsUsed(string? question, string expected)
    {
        Assert.Equal(expected, AssistantPrompt.NormalizeQuestion(question));
    }

    [Fact]
    public void AnOversizedQuestionIsCutRatherThanRefused()
    {
        var question = new string('a', AssistantPrompt.MaximumQuestionLength + 500);

        var normalized = AssistantPrompt.NormalizeQuestion(question);

        Assert.Equal(AssistantPrompt.MaximumQuestionLength, normalized.Length);
    }

    private static AnalysisBriefing Briefing() => new()
    {
        RepositoryName = "Storefront",
        Baseline = "main",
        CapturedAt = new DateTimeOffset(2026, 9, 2, 10, 0, 0, TimeSpan.Zero),
        Policy = new BriefingPolicy
        {
            ActiveUntilDays = 30,
            InactiveAfterDays = 90,
            ProtectedPatterns = [],
            ExcludedPatterns = [],
        },
        Branches = [],
    };
}
