using System.Text.Json.Nodes;

namespace App.GitHealth.Api.Features.Assistant.Mcp;

/// <summary>
/// What one tool call produced. A refusal travels as a result rather than as a protocol
/// error on purpose: the agent is meant to read it, correct itself and call again, which it
/// cannot do with a transport failure.
/// </summary>
internal sealed record AssistantMcpToolResult
{
    private const string TextContent = "text";

    private AssistantMcpToolResult(string text, bool isError)
    {
        Text = text;
        IsError = isError;
    }

    public string Text { get; }

    public bool IsError { get; }

    public static AssistantMcpToolResult Success(JsonNode payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new AssistantMcpToolResult(payload.ToJsonString(), isError: false);
    }

    public static AssistantMcpToolResult Error(string message) => new(message, isError: true);

    /// <summary>Wraps the outcome in the shape the protocol expects of a tool call.</summary>
    public JsonNode ToContent() => new JsonObject
    {
        ["content"] = new JsonArray(new JsonObject
        {
            ["type"] = TextContent,
            ["text"] = Text,
        }),
        ["isError"] = IsError,
    };
}
