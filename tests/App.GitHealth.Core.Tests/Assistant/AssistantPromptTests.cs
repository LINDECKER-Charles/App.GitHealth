using App.GitHealth.Core.Assistant;

namespace App.GitHealth.Core.Tests.Assistant;

public sealed class AssistantPromptTests
{
    private const string Question = "Which branches can I clean up?";

    /// <summary>
    /// The tools are the only way the capture reaches the agent now that it is no longer
    /// pasted in. A prompt that stops naming one leaves that reading unreachable.
    /// </summary>
    [Theory]
    [InlineData("`get_capture`")]
    [InlineData("`list_branches`")]
    [InlineData("`get_branch`")]
    [InlineData("`count_branches`")]
    public void ThePromptNamesEveryToolTheAgentIsGiven(string tool)
    {
        Assert.Contains(tool, AssistantPrompt.Compose(Question), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePromptDescribesTheToolsBeforeItAsksTheQuestion()
    {
        var prompt = AssistantPrompt.Compose(Question);

        var tools = prompt.IndexOf("## Tools", StringComparison.Ordinal);
        var question = prompt.IndexOf("## Question", StringComparison.Ordinal);
        Assert.True(tools >= 0);
        Assert.True(question > tools);
        Assert.Contains(Question, prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// The framing is the only thing keeping the agent inside the capture. If it ever stops
    /// being stated, the agent starts inventing branches instead of reading them.
    /// </summary>
    [Fact]
    public void ThePromptTellsTheAgentItCannotOpenTheRepository()
    {
        var prompt = AssistantPrompt.Compose("Anything to clean?");

        Assert.Contains(
            "You cannot open the repository and you have no other tool.",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains("Never invent one.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePromptForbidsActingOnTheRepository()
    {
        var prompt = AssistantPrompt.Compose("Anything to clean?");

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

    /// <summary>
    /// The prompt is what actually leaves the machine, so the cap has to hold there and not
    /// only in the value a caller thought to normalize first.
    /// </summary>
    [Fact]
    public void TheComposedPromptCarriesTheCutQuestionRatherThanTheWholeOne()
    {
        var question = new string('a', AssistantPrompt.MaximumQuestionLength + 500);

        var prompt = AssistantPrompt.Compose(question);

        Assert.DoesNotContain(question, prompt, StringComparison.Ordinal);
        Assert.Contains(
            question[..AssistantPrompt.MaximumQuestionLength],
            prompt,
            StringComparison.Ordinal);
    }
}
