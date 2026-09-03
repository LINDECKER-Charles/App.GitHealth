using System.Text.Json.Nodes;

namespace App.GitHealth.Api.Features.Assistant.Agents.Events;

/// <summary>
/// Reads Codex CLI's <c>--json</c> output. Its items are announced whole rather than
/// streamed, so a tool call is shown when it starts and a message when it has been written.
/// </summary>
/// <remarks>
/// Codex does summarise its own reasoning when the model produces a summary, and that
/// summary is shown as it comes. It is the one place either agent says anything about how it
/// is thinking, so it is carried through rather than reduced to "thinking".
/// </remarks>
internal sealed class CodexEventReader : IAgentEventReader
{
    /// <summary>Two messages of one turn are two paragraphs, not one run-on line.</summary>
    private const string Separator = "\n\n";

    public AgentEvent Read(JsonNode line) => AgentEventJson.Text(line, "type") switch
    {
        "turn.started" => AgentEvent.Step(new AgentStep(AgentStepKind.Waiting)),
        "item.started" => ReadStarted(AgentEventJson.Object(line, "item")),
        "item.completed" => ReadCompleted(AgentEventJson.Object(line, "item")),
        _ => AgentEvent.None,
    };

    private static AgentEvent ReadStarted(JsonObject? item) =>
        AgentEventJson.Text(item, "type") == "mcp_tool_call"
            ? AgentEvent.Step(ReadCall(item))
            : AgentEvent.None;

    private static AgentEvent ReadCompleted(JsonObject? item) =>
        AgentEventJson.Text(item, "type") switch
        {
            "agent_message" => ReadMessage(AgentEventJson.Text(item, "text")),
            "reasoning" => AgentEvent.Step(new AgentStep(
                AgentStepKind.Thinking,
                string.Empty,
                AgentEventJson.Shorten(AgentEventJson.Text(item, "text")))),
            _ => AgentEvent.None,
        };

    private static AgentEvent ReadMessage(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? AgentEvent.None
            : new AgentEvent
            {
                Steps = [new AgentStep(AgentStepKind.Writing)],
                Written = text + Separator,
            };

    private static AgentStep ReadCall(JsonObject? item) => new(
        AgentStepKind.Tool,
        AgentEventJson.Text(item, "tool") ?? string.Empty,
        AgentEventJson.Describe(item?["arguments"]));
}
