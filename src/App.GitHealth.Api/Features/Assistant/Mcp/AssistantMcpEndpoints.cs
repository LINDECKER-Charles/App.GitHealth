using System.Text.Json;
using System.Text.Json.Nodes;

namespace App.GitHealth.Api.Features.Assistant.Mcp;

/// <summary>
/// The bridge an agent talks to. It sits outside <c>/api</c> deliberately: that prefix is
/// reserved for the browser and is guarded by a session cookie and an antiforgery token,
/// neither of which a command-line agent has. This route answers to one thing instead — the
/// single-run token in its path — and serves nothing but the capture that token was cut for.
/// </summary>
internal static class AssistantMcpEndpoints
{
    public const string RoutePrefix = "/agent-bridge";

    private const string RouteTemplate = RoutePrefix + "/{token}";
    private const string JsonContentType = "application/json";

    /// <summary>A whole conversation with the bridge is small; a huge body is not one.</summary>
    private const int MaximumRequestBytes = 256 * 1024;

    public static IEndpointRouteBuilder MapAssistantMcpEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(RouteTemplate, HandleAsync).ExcludeFromDescription();
        endpoints.MapGet(RouteTemplate, NoStream).ExcludeFromDescription();
        endpoints.MapDelete(RouteTemplate, Close).ExcludeFromDescription();
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(string token, HttpContext context)
    {
        var registry = context.RequestServices
            .GetRequiredService<AssistantMcpSessionRegistry>();
        var session = registry.Find(token);
        if (session is null)
        {
            return Results.Json(
                AssistantMcpDispatcher.InvalidRequest("This bridge token is not open."),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        using var document = await ReadAsync(context);
        if (document is null)
        {
            return Results.Json(
                AssistantMcpDispatcher.InvalidRequest(
                    "The body could not be read as JSON-RPC."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Answer(Dispatch(session, document.RootElement));
    }

    private static List<JsonNode> Dispatch(AssistantMcpSession session, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            var single = AssistantMcpDispatcher.Dispatch(session.Capture, root);
            return single is null ? [] : [single];
        }

        var replies = new List<JsonNode>();
        foreach (var message in root.EnumerateArray())
        {
            var reply = AssistantMcpDispatcher.Dispatch(session.Capture, message);
            if (reply is not null)
            {
                replies.Add(reply);
            }
        }

        return replies;
    }

    /// <summary>
    /// Notifications carry no answer at all, and the protocol says so with a 202 rather than
    /// with an empty body a client would try to parse.
    /// </summary>
    private static IResult Answer(List<JsonNode> replies) => replies.Count switch
    {
        0 => Results.StatusCode(StatusCodes.Status202Accepted),
        1 => Results.Text(replies[0].ToJsonString(), JsonContentType),
        _ => Results.Text(new JsonArray([.. replies]).ToJsonString(), JsonContentType),
    };

    private static async Task<JsonDocument?> ReadAsync(HttpContext context)
    {
        try
        {
            context.Request.Body = new MemoryStream(await ReadBytesAsync(context));
            return await JsonDocument.ParseAsync(
                context.Request.Body,
                default,
                context.RequestAborted);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<byte[]> ReadBytesAsync(HttpContext context)
    {
        using var buffer = new MemoryStream();
        await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
        var bytes = buffer.ToArray();
        return bytes.Length <= MaximumRequestBytes ? bytes : [];
    }

    /// <summary>
    /// Clients open a stream to receive messages the server pushes on its own. This bridge
    /// only ever answers what it was asked, and the protocol spells that refusal as a 405.
    /// </summary>
    private static IResult NoStream() =>
        Results.StatusCode(StatusCodes.Status405MethodNotAllowed);

    /// <summary>The session ends with the run, so a client closing it has nothing to undo.</summary>
    private static IResult Close() => Results.StatusCode(StatusCodes.Status204NoContent);
}
