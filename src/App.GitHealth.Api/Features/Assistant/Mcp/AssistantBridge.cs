using System.Text.Json.Nodes;
using App.GitHealth.Api.Hosting;
using App.GitHealth.Core.Assistant;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace App.GitHealth.Api.Features.Assistant.Mcp;

/// <summary>
/// Opens and closes the door an agent reads a capture through. One run gets one token and
/// one address; the address is always loopback and the port is the one the interface is
/// already served on, so nothing new is listening and nothing is reachable off the machine.
/// </summary>
internal sealed class AssistantBridge(IServer server, AssistantMcpSessionRegistry sessions)
{
    private const string ServerKey = "mcpServers";
    private const string TransportType = "http";

    public AssistantBridgeTicket Open(
        AssistantRunKey key,
        AnalysisBriefing capture)
    {
        ArgumentNullException.ThrowIfNull(key);
        var session = sessions.Open(key.RunId, key.ProjectId, capture);
        return new AssistantBridgeTicket(session.Token, Address(session.Token));
    }

    public void Close(string token) => sessions.Close(token);

    /// <summary>
    /// The declaration Claude Code reads its servers from, passed inline on the command line
    /// so the token never lands in a file. Codex reads the same address from an override.
    /// </summary>
    public static string DescribeForClaude(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return new JsonObject
        {
            [ServerKey] = new JsonObject
            {
                [AssistantPrompt.ToolNamespace] = new JsonObject
                {
                    ["type"] = TransportType,
                    ["url"] = address.ToString(),
                },
            },
        }.ToJsonString();
    }

    private Uri Address(string token) => new(
        LauncherOptions.CreateApplicationAddress(BoundPort()),
        $"{AssistantMcpEndpoints.RoutePrefix}/{token}");

    private int BoundPort()
    {
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        var bound = addresses?
            .Select(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null)
            .FirstOrDefault(uri => uri is not null && uri.Port > 0);
        return bound?.Port ?? throw new InvalidOperationException(
            "The port the interface is served on cannot be found, so no agent can be told"
            + " where to read the capture.");
    }
}

/// <summary>Which run, of which project, a bridge session belongs to.</summary>
internal sealed record AssistantRunKey(Guid RunId, Guid ProjectId);

/// <summary>The secret and the address one run was given, held together so neither leaks alone.</summary>
internal sealed record AssistantBridgeTicket(string Token, Uri Address);
