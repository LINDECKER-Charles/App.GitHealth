using System.Text.Json;
using System.Text.Json.Nodes;
using App.GitHealth.Core.Assistant;

namespace App.GitHealth.Api.Features.Assistant.Mcp;

/// <summary>
/// Answers the handful of JSON-RPC methods an agent asks of a tool server. The surface is
/// deliberately the minimum the two supported CLIs use: a method outside this list is
/// refused by name rather than quietly answered with something plausible.
/// </summary>
internal static class AssistantMcpDispatcher
{
    private const string DefaultProtocolVersion = "2025-06-18";
    private const string ServerVersion = "1";
    private const int MethodNotFound = -32601;
    private const int InvalidParameters = -32602;

    public static JsonNode? Dispatch(AnalysisBriefing capture, JsonElement message)
    {
        ArgumentNullException.ThrowIfNull(capture);
        if (!message.TryGetProperty("id", out var id))
        {
            return null; // A notification. The protocol expects no answer at all.
        }

        var method = Method(message);
        var parameters = Parameters(message);
        return method switch
        {
            "initialize" => Reply(id, Handshake(parameters)),
            "tools/list" => Reply(id, new JsonObject
            {
                ["tools"] = AssistantMcpTools.Declare(),
            }),
            "tools/call" => Reply(id, Call(capture, parameters)),
            "ping" => Reply(id, new JsonObject()),
            "resources/list" => Reply(id, new JsonObject { ["resources"] = new JsonArray() }),
            "prompts/list" => Reply(id, new JsonObject { ["prompts"] = new JsonArray() }),
            _ => Failure(id, MethodNotFound, $"There is no \"{method}\" method here."),
        };
    }

    /// <summary>
    /// Echoes the version the client asked for. Both supported CLIs move faster than this
    /// bridge, and the surface used here has not changed across the versions they speak.
    /// </summary>
    private static JsonObject Handshake(JsonElement parameters) => new JsonObject
    {
        ["protocolVersion"] = ProtocolVersion(parameters),
        ["capabilities"] = new JsonObject
        {
            ["tools"] = new JsonObject { ["listChanged"] = false },
        },
        ["serverInfo"] = new JsonObject
        {
            ["name"] = AssistantPrompt.ToolNamespace,
            ["version"] = ServerVersion,
        },
    };

    private static JsonNode Call(AnalysisBriefing capture, JsonElement parameters)
    {
        var name = Text(parameters, "name");
        if (!AssistantMcpTools.IsKnown(name))
        {
            return AssistantMcpToolResult
                .Error($"There is no \"{name}\" tool. Call tools/list to see what there is.")
                .ToContent();
        }

        var arguments = parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("arguments", out var value)
                ? value
                : default;
        return AssistantMcpToolRunner.Run(capture, name!, arguments).ToContent();
    }

    private static JsonObject Reply(JsonElement id, JsonNode result) => new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = Identifier(id),
        ["result"] = result,
    };

    public static JsonNode Failure(JsonElement id, int code, string message) => new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = Identifier(id),
        ["error"] = new JsonObject
        {
            ["code"] = code,
            ["message"] = message,
        },
    };

    public static JsonNode InvalidRequest(string message) =>
        Failure(default, InvalidParameters, message);

    private static JsonValue? Identifier(JsonElement id) => id.ValueKind switch
    {
        JsonValueKind.String => JsonValue.Create(id.GetString()),
        JsonValueKind.Number when id.TryGetInt64(out var number) => JsonValue.Create(number),
        _ => null,
    };

    private static string? Method(JsonElement message) => Text(message, "method");

    private static JsonElement Parameters(JsonElement message) =>
        message.TryGetProperty("params", out var value) ? value : default;

    private static string ProtocolVersion(JsonElement parameters) =>
        Text(parameters, "protocolVersion") ?? DefaultProtocolVersion;

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
