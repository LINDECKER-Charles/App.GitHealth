using System.Text.Json.Nodes;

namespace App.GitHealth.Api.Features.Assistant.Agents.Events;

/// <summary>
/// Reads fields out of somebody else's JSON. Every lookup answers null rather than throwing:
/// these are the event formats of two CLIs that ship on their own schedule, and a field that
/// moved should cost a line of the trace, never the run.
/// </summary>
internal static class AgentEventJson
{
    /// <summary>The panel is a column, not a terminal: more than this is noise.</summary>
    private const int MaximumDetailLength = 120;

    private const string Ellipsis = "…";

    public static JsonObject? Object(JsonNode? node, string name) =>
        (node as JsonObject)?[name] as JsonObject;

    public static string? Text(JsonNode? node, string name)
    {
        var value = (node as JsonObject)?[name] as JsonValue;
        return value is not null && value.TryGetValue<string>(out var text) ? text : null;
    }

    /// <summary>
    /// The arguments of a tool call, as the panel shows them: <c>verdict=merged, take=50</c>.
    /// What the agent asked for is the interesting half of a call — "reading the branches"
    /// says much less than which branches it went looking for.
    /// </summary>
    public static string? Describe(JsonNode? arguments)
    {
        if (arguments is not JsonObject fields || fields.Count == 0)
        {
            return null;
        }

        return Shorten(string.Join(
            ", ",
            fields.Select(field => $"{field.Key}={Flatten(field.Value)}")));
    }

    /// <summary>Keeps the beginning, which is where an agent says what it is up to.</summary>
    public static string? Shorten(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= MaximumDetailLength
            ? trimmed
            : trimmed[..MaximumDetailLength] + Ellipsis;
    }

    /// <summary>A string reads as itself; anything else reads as the JSON it was.</summary>
    private static string Flatten(JsonNode? value)
    {
        if (value is JsonValue single && single.TryGetValue<string>(out var text))
        {
            return text;
        }

        return value?.ToJsonString() ?? "null";
    }
}
