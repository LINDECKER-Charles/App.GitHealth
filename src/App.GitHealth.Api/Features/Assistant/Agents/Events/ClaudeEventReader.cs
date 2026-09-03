using System.Text.Json.Nodes;
using App.GitHealth.Core.Assistant;

namespace App.GitHealth.Api.Features.Assistant.Agents.Events;

/// <summary>
/// Reads Claude Code's <c>stream-json</c> output. Thinking and writing are taken from the
/// block that opens them, because the point of watching a run is seeing it start; tool calls
/// are taken from the message that completes them, because their arguments arrive as a
/// stream of fragments and a call is worth showing with what it asked for.
/// </summary>
/// <remarks>
/// The reasoning itself is not in this stream. Claude Code streams the length of a thinking
/// block, never its text, so a step says that the model is thinking and stops there rather
/// than inventing a summary of something it cannot read.
/// </remarks>
internal sealed class ClaudeEventReader : IAgentEventReader
{
    /// <summary>Every tool a run can call is the bridge's, so the namespace is noise.</summary>
    private const string ToolPrefix = $"mcp__{AssistantPrompt.ToolNamespace}__";

    public AgentEvent Read(JsonNode line) => AgentEventJson.Text(line, "type") switch
    {
        "system" => ReadStatus(line),
        "stream_event" => ReadStream(AgentEventJson.Object(line, "event")),
        "assistant" => ReadCalls(line),
        "result" => new AgentEvent { Answer = AgentEventJson.Text(line, "result") },
        _ => AgentEvent.None,
    };

    private static AgentEvent ReadStatus(JsonNode line) =>
        AgentEventJson.Text(line, "subtype") == "status"
        && AgentEventJson.Text(line, "status") == "requesting"
            ? AgentEvent.Step(new AgentStep(AgentStepKind.Waiting))
            : AgentEvent.None;

    private static AgentEvent ReadStream(JsonObject? streamed) =>
        AgentEventJson.Text(streamed, "type") switch
        {
            "content_block_start" => ReadBlock(AgentEventJson.Object(streamed, "content_block")),
            "content_block_delta" => ReadDelta(AgentEventJson.Object(streamed, "delta")),
            _ => AgentEvent.None,
        };

    private static AgentEvent ReadBlock(JsonObject? block) =>
        AgentEventJson.Text(block, "type") switch
        {
            "thinking" => AgentEvent.Step(new AgentStep(AgentStepKind.Thinking)),
            "text" => AgentEvent.Step(new AgentStep(AgentStepKind.Writing)),
            _ => AgentEvent.None,
        };

    private static AgentEvent ReadDelta(JsonObject? delta) =>
        AgentEventJson.Text(delta, "type") == "text_delta"
            ? new AgentEvent { Written = AgentEventJson.Text(delta, "text") }
            : AgentEvent.None;

    /// <summary>The calls of one message, which is where their arguments are complete.</summary>
    private static AgentEvent ReadCalls(JsonNode line)
    {
        var content = AgentEventJson.Object(line, "message")?["content"] as JsonArray;
        var calls = content?
            .Where(block => AgentEventJson.Text(block, "type") == "tool_use")
            .Select(ReadCall)
            .ToArray() ?? [];
        return calls.Length == 0 ? AgentEvent.None : new AgentEvent { Steps = calls };
    }

    private static AgentStep ReadCall(JsonNode? block) => new(
        AgentStepKind.Tool,
        ShortName(AgentEventJson.Text(block, "name")),
        AgentEventJson.Describe((block as JsonObject)?["input"]));

    private static string ShortName(string? name)
    {
        if (name is null)
        {
            return string.Empty;
        }

        return name.StartsWith(ToolPrefix, StringComparison.Ordinal)
            ? name[ToolPrefix.Length..]
            : name;
    }
}
