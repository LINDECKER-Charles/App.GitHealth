using App.GitHealth.Api.Features.Assistant.Agents;
using App.GitHealth.Api.Features.Assistant.Agents.Events;

namespace App.GitHealth.Api.Tests.Assistant;

/// <summary>
/// The lines here are the ones the installed CLIs actually printed, trimmed of the fields
/// nothing reads. Both formats belong to somebody else and move on their own schedule, so
/// what is asserted is that a real run reads as steps a reader can follow.
/// </summary>
public sealed class AgentEventStreamTests
{
    private const string ClaudeRun = """
        {"type":"system","subtype":"init","tools":[],"model":"claude-opus-5"}
        {"type":"system","subtype":"status","status":"requesting"}
        {"type":"stream_event","event":{"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":"","signature":""}}}
        {"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"","estimated_tokens":50}}}
        {"type":"assistant","message":{"content":[{"type":"tool_use","id":"toolu_01","name":"mcp__githealth__list_branches","input":{"verdict":"cleanup candidate","take":50}}]}}
        {"type":"stream_event","event":{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}}
        {"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Two branches "}}}
        {"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"can go."}}}
        {"type":"rate_limit_event","rate_limit_info":{"status":"allowed"}}
        {"type":"result","subtype":"success","is_error":false,"result":"Two branches can go."}

        """;

    private const string CodexRun = """
        {"type":"thread.started","thread_id":"01a06654"}
        {"type":"turn.started"}
        {"type":"item.completed","item":{"id":"item_0","type":"agent_message","text":"I'll read the capture first."}}
        {"type":"item.started","item":{"id":"item_1","type":"mcp_tool_call","server":"githealth","tool":"list_branches","arguments":{"verdict":"merged"},"result":null,"status":"in_progress"}}
        {"type":"item.completed","item":{"id":"item_1","type":"mcp_tool_call","server":"githealth","tool":"list_branches","arguments":{"verdict":"merged"},"status":"completed"}}
        {"type":"item.completed","item":{"id":"item_2","type":"agent_message","text":"Two branches can go."}}
        {"type":"turn.completed","usage":{"output_tokens":198}}

        """;

    [Fact]
    public void AClaudeRunReadsAsTheStepsItWentThrough()
    {
        var read = Read("claude", ClaudeRun);

        Assert.Equal(
            [
                AgentStepKind.Waiting,
                AgentStepKind.Thinking,
                AgentStepKind.Tool,
                AgentStepKind.Writing,
            ],
            read.Steps.Select(step => step.Kind));
    }

    /// <summary>
    /// Which branches it went looking for is the interesting half of a call: "reading the
    /// branches" says far less than the filter it read them with.
    /// </summary>
    [Fact]
    public void AToolCallCarriesWhatItAskedFor()
    {
        var steps = Read("claude", ClaudeRun).Steps;

        var call = Assert.Single(steps, step => step.Kind == AgentStepKind.Tool);

        Assert.Equal("list_branches", call.Label);
        Assert.Equal("verdict=cleanup candidate, take=50", call.Detail);
    }

    [Fact]
    public void TheAnswerIsTheOneTheAgentReports()
    {
        Assert.Equal("Two branches can go.", Read("claude", ClaudeRun).Stream.Answer);
    }

    /// <summary>
    /// A read of a pipe stops wherever the buffer ends, which is very often mid-line and
    /// occasionally mid-character of a name. An event split across two reads is still one
    /// event, and a run that showed half its steps would be worse than showing none.
    /// </summary>
    [Fact]
    public void EventsSplitAcrossReadsAreStillReadWhole()
    {
        var whole = Read("claude", ClaudeRun);

        var pieces = Read("claude", ClaudeRun, chunkSize: 7);

        Assert.Equal(whole.Steps, pieces.Steps);
        Assert.Equal(whole.Stream.Answer, pieces.Stream.Answer);
    }

    /// <summary>A CLI may print a warning of its own; it is simply not an event.</summary>
    [Fact]
    public void ALineThatIsNotJsonIsSkippedRatherThanFailingTheRun()
    {
        var read = Read("claude", "npm warn: a new version is available\n" + ClaudeRun);

        Assert.Equal("Two branches can go.", read.Stream.Answer);
    }

    /// <summary>
    /// What the agent wrote is what is left to read when it is stopped before it can report
    /// an answer, so it is kept as it is written rather than reconstructed afterwards.
    /// </summary>
    [Fact]
    public void WhatTheAgentWroteIsKeptForARunThatNeverReportsAnAnswer()
    {
        var stopped = ClaudeRun.IndexOf("\n{\"type\":\"result\"", StringComparison.Ordinal);

        var read = Read("claude", ClaudeRun[..stopped]);

        Assert.Null(read.Stream.Answer);
        Assert.Equal("Two branches can go.", read.Stream.Written);
    }

    [Fact]
    public void ACodexRunReadsAsTheStepsItWentThrough()
    {
        var read = Read("codex", CodexRun);

        Assert.Equal(
            [
                AgentStepKind.Waiting,
                AgentStepKind.Writing,
                AgentStepKind.Tool,
                AgentStepKind.Writing,
            ],
            read.Steps.Select(step => step.Kind));
    }

    [Fact]
    public void ACodexToolCallIsShownWhenItStartsRatherThanWhenItReturns()
    {
        var steps = Read("codex", CodexRun).Steps;

        var call = Assert.Single(steps, step => step.Kind == AgentStepKind.Tool);

        Assert.Equal("list_branches", call.Label);
        Assert.Equal("verdict=merged", call.Detail);
    }

    /// <summary>Codex reports its answer in the file it was given, never in its stream.</summary>
    [Fact]
    public void ACodexRunLeavesItsAnswerToTheFileItWasGiven()
    {
        var read = Read("codex", CodexRun);

        Assert.Null(read.Stream.Answer);
        Assert.Contains("Two branches can go.", read.Stream.Written, StringComparison.Ordinal);
    }

    private static Reading Read(string agentId, string output, int chunkSize = 0)
    {
        var steps = new List<AgentStep>();
        var written = new List<string>();
        var stream = AgentEventStream.For(
            AgentCatalog.Find(agentId)!,
            steps.Add,
            written.Add);
        foreach (var chunk in Split(output, chunkSize))
        {
            stream.Report(chunk);
        }

        return new Reading(stream, steps);
    }

    private static IEnumerable<string> Split(string output, int chunkSize)
    {
        if (chunkSize <= 0)
        {
            return [output];
        }

        return Enumerable
            .Range(0, (output.Length + chunkSize - 1) / chunkSize)
            .Select(index => output.Substring(
                index * chunkSize,
                Math.Min(chunkSize, output.Length - (index * chunkSize))));
    }

    private sealed record Reading(AgentEventStream Stream, IReadOnlyList<AgentStep> Steps);
}
