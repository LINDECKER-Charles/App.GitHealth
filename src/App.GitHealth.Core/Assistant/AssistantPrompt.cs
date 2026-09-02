using System.Text;

namespace App.GitHealth.Core.Assistant;

/// <summary>
/// Assembles what is handed to the agent: its brief, the capture, then the question. The
/// framing is deliberately narrow — the agent reads a table and argues about it, it does
/// not go looking for the repository.
/// </summary>
public static class AssistantPrompt
{
    public const int MaximumQuestionLength = 2000;

    private static readonly string[] Rules =
    [
        "Answer from the capture alone. It is complete for what it covers; when it does not"
            + " hold what the question needs, say which fact is missing instead of guessing.",
        "Name branches exactly as they appear in the table. Never invent one.",
        "GitHealth deletes, merges and pushes nothing, and neither do you. Recommend an"
            + " action if you have one — the reader is the one who runs it.",
        "Be short and specific. A named list of branches beats a paragraph about them.",
        "You may disagree with a verdict when the row's own facts justify it. Say that you"
            + " are disagreeing, and on which fact.",
        "Reply in plain Markdown, with no preamble and no restating of the question.",
    ];

    public static string Compose(AnalysisBriefing briefing, string question)
    {
        ArgumentNullException.ThrowIfNull(briefing);
        var builder = new StringBuilder();
        WriteBrief(builder);
        builder.AppendLine(BriefingWriter.Write(briefing));
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
            + " GitHealth — a local tool that measures them without touching them. Everything"
            + " you know about this repository is in the capture below: you have no access to"
            + " the repository, and no tool call is expected of you.");
        builder.AppendLine();
        foreach (var rule in Rules)
        {
            builder.AppendLine("- " + rule);
        }

        builder.AppendLine();
    }

    private static void WriteQuestion(StringBuilder builder, string question)
    {
        builder.AppendLine();
        builder.AppendLine("## Question");
        builder.AppendLine();
        builder.AppendLine(NormalizeQuestion(question));
    }
}
