using System.Text;

namespace App.GitHealth.Core.Assistant;

/// <summary>
/// Assembles what is handed to the agent: its brief, the tools it may call, then the
/// question. The capture itself is no longer pasted in — the agent reads it through the
/// tools, so it asks for the rows it needs instead of being handed all of them.
/// </summary>
public static class AssistantPrompt
{
    public const int MaximumQuestionLength = 2000;

    /// <summary>Name the tools are published under, shared with the bridge that serves them.</summary>
    public const string ToolNamespace = "githealth";

    public const string CaptureTool = "get_capture";
    public const string ListTool = "list_branches";
    public const string BranchTool = "get_branch";
    public const string CountTool = "count_branches";

    private static readonly string[] Tools =
    [
        $"`{CaptureTool}` — the repository, the baseline, the moment of the capture, how many"
            + " branches it holds, the policy in force and how to read a row.",
        $"`{ListTool}` — the measured branches. Filters on verdict, topology, activity,"
            + " author, flags and name fragment; pages through what matches.",
        $"`{BranchTool}` — every measurement GitHealth holds for one branch.",
        $"`{CountTool}` — how many branches fall in each verdict, topology, activity or"
            + " author, without reading the rows one by one.",
    ];

    private static readonly string[] Rules =
    [
        "Answer from what the tools return. They are complete for what they cover; when they"
            + " do not hold what the question needs, say which fact is missing rather than"
            + " guessing at it.",
        "Name branches exactly as the tools spell them. Never invent one.",
        "GitHealth deletes, merges and pushes nothing, and neither do you. Recommend an"
            + " action if you have one — the reader is the one who runs it.",
        "Be short and specific. A named list of branches beats a paragraph about them.",
        "You may disagree with a verdict when the branch's own facts justify it. Say that you"
            + " are disagreeing, and on which fact.",
        "Reply in plain Markdown, with no preamble and no restating of the question. Write"
            + " every branch name as inline code so the reader can open its row.",
    ];

    /// <summary>
    /// The instructions and the question. The capture reaches the agent over the bridge, so
    /// the size of this text no longer grows with the number of branches measured.
    /// </summary>
    public static string Compose(string question)
    {
        var builder = new StringBuilder();
        WriteBrief(builder);
        WriteTools(builder);
        WriteRules(builder);
        WriteQuestion(builder, question);
        return builder.ToString();
    }

    /// <summary>
    /// Trims a question to what the prompt will carry. A caller sending more is not refused:
    /// the excess is dropped, since a truncated question still reads as a question.
    /// </summary>
    public static string NormalizeQuestion(string? question)
    {
        var trimmed = (question ?? string.Empty).Trim();
        return trimmed.Length <= MaximumQuestionLength
            ? trimmed
            : trimmed[..MaximumQuestionLength].TrimEnd();
    }

    private static void WriteBrief(StringBuilder builder)
    {
        builder.AppendLine(
            "You are answering a question about the branches of a Git repository, inside"
            + " GitHealth — a local tool that measures them without touching them. You cannot"
            + " open the repository and you have no other tool. What you can read is one"
            + $" capture GitHealth already took, served by the `{ToolNamespace}` tools.");
        builder.AppendLine();
    }

    private static void WriteTools(StringBuilder builder)
    {
        builder.AppendLine("## Tools");
        builder.AppendLine();
        foreach (var tool in Tools)
        {
            builder.AppendLine("- " + tool);
        }

        builder.AppendLine();
        builder.AppendLine(
            $"Call `{CaptureTool}` first: it says how many branches the capture holds and what"
            + " each column means. Then read only what the question needs.");
        builder.AppendLine();
    }

    private static void WriteRules(StringBuilder builder)
    {
        builder.AppendLine("## Rules");
        builder.AppendLine();
        foreach (var rule in Rules)
        {
            builder.AppendLine("- " + rule);
        }

        builder.AppendLine();
    }

    private static void WriteQuestion(StringBuilder builder, string question)
    {
        builder.AppendLine("## Question");
        builder.AppendLine();
        builder.AppendLine(NormalizeQuestion(question));
    }
}
