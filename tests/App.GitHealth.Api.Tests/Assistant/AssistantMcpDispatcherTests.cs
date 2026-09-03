using System.Text.Json;
using System.Text.Json.Nodes;
using App.GitHealth.Api.Features.Assistant.Mcp;
using App.GitHealth.Core.Assistant;

namespace App.GitHealth.Api.Tests.Assistant;

public sealed class AssistantMcpDispatcherTests
{
    private const int MethodNotFound = -32601;

    private static readonly AnalysisBriefing Capture = AssistantTestCapture.Create();

    /// <summary>
    /// Both supported CLIs move faster than this bridge, and the surface used here has not
    /// changed across the versions they speak, so the handshake follows the client rather
    /// than announcing a version the client would then have to accommodate.
    /// </summary>
    [Fact]
    public void TheHandshakeEchoesTheProtocolVersionTheClientAskedFor()
    {
        var reply = Dispatch(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize",
             "params":{"protocolVersion":"2024-11-05"}}
            """)!;

        Assert.Equal("2024-11-05", reply["result"]!["protocolVersion"]!.GetValue<string>());
        Assert.Equal(1, reply["id"]!.GetValue<long>());
    }

    [Fact]
    public void TheBridgePublishesItsFourReadingToolsAndNothingElse()
    {
        var reply = Dispatch("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""")!;

        var names = reply["result"]!["tools"]!.AsArray()
            .Select(tool => tool!["name"]!.GetValue<string>());
        Assert.Equal(
            [
                AssistantMcpTools.GetCapture,
                AssistantMcpTools.ListBranches,
                AssistantMcpTools.GetBranch,
                AssistantMcpTools.CountBranches,
            ],
            names);
    }

    /// <summary>
    /// An agent asked to count must know it is reading a truncated list, so the capture
    /// states what the size cap left out instead of hiding it.
    /// </summary>
    [Fact]
    public void TheCaptureStatesHowManyBranchesItLeftOut()
    {
        var payload = Payload(Call(AssistantMcpTools.GetCapture, "{}"));

        Assert.Equal(
            Capture.MeasuredBranchCount,
            payload["branchesMeasured"]!.GetValue<int>());
        Assert.Equal(Capture.Branches.Count, payload["branchesReadable"]!.GetValue<int>());
        Assert.Equal(
            AssistantTestCapture.OmittedBranches,
            payload["branchesOmitted"]!.GetValue<int>());
    }

    /// <summary>
    /// A value the capture holds for nobody matches nothing rather than everything: the
    /// agent notices an empty page, a silent fallback to the whole table it would not.
    /// </summary>
    [Theory]
    [InlineData("""{"verdict":"CleanupCandidate"}""", 2)]
    [InlineData("""{"author":"hopper"}""", 2)]
    [InlineData("""{"nameContains":"feature/"}""", 2)]
    [InlineData("""{"isProtected":true}""", 1)]
    [InlineData("""{"isExcluded":true}""", 1)]
    [InlineData("""{"verdict":"Retire"}""", 0)]
    public void ListingBranchesKeepsOnlyWhatEveryFilterAccepts(string filter, int expected)
    {
        var payload = Payload(Call(AssistantMcpTools.ListBranches, filter));

        Assert.Equal(expected, payload["matched"]!.GetValue<int>());
        Assert.Equal(expected, payload["branches"]!.AsArray().Count);
    }

    [Fact]
    public void ListingBranchesPagesWithoutLosingHowManyMatched()
    {
        var payload = Payload(Call(AssistantMcpTools.ListBranches, """{"skip":1,"take":2}"""));

        Assert.Equal(Capture.Branches.Count, payload["matched"]!.GetValue<int>());
        Assert.Equal(1, payload["skip"]!.GetValue<int>());
        Assert.Equal(2, payload["returned"]!.GetValue<int>());
        var page = payload["branches"]!.AsArray()
            .Select(branch => branch!["branch"]!.GetValue<string>());
        Assert.Equal(
            [Capture.Branches[1].ReferenceName, Capture.Branches[2].ReferenceName],
            page);
    }

    [Fact]
    public void OneBranchReadsBackWithTheMeasurementsHeldForIt()
    {
        var branch = Capture.Branches[0];

        var reply = Call(
            AssistantMcpTools.GetBranch,
            $$"""{"branch":"{{branch.ReferenceName}}"}""");

        var payload = Payload(reply);
        Assert.False(IsError(reply));
        Assert.Equal(branch.AheadCount, payload["ahead"]!.GetValue<int>());
        Assert.Equal(branch.Recommendation, payload["verdict"]!.GetValue<string>());
        Assert.Equal(branch.TipAuthor, payload["author"]!.GetValue<string>());
    }

    /// <summary>
    /// A refusal travels as a result rather than as a protocol error on purpose: the agent
    /// is meant to read it, correct the name and call again, which a transport failure would
    /// leave it no way to do.
    /// </summary>
    [Fact]
    public void AnUnknownBranchIsRefusedInsideTheResultRatherThanAsAProtocolError()
    {
        var reply = Call(AssistantMcpTools.GetBranch, """{"branch":"refs/heads/ghost"}""");

        Assert.Null(reply["error"]);
        Assert.True(IsError(reply));
        Assert.Contains(AssistantMcpTools.ListBranches, Text(reply), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("verdict", 4)]
    [InlineData("topology", 3)]
    [InlineData("activity", 3)]
    [InlineData("author", 3)]
    public void CountingGroupsTheWholeCaptureByOneField(string field, int expected)
    {
        var reply = Call(AssistantMcpTools.CountBranches, $$"""{"groupBy":"{{field}}"}""");

        var payload = Payload(reply);
        Assert.Equal(Capture.Branches.Count, payload["over"]!.GetValue<int>());
        Assert.Equal(expected, payload["groups"]!.AsArray().Count);
    }

    /// <summary>
    /// The point of counting is the shape of the capture, so the biggest group leads.
    /// </summary>
    [Fact]
    public void TheLargestGroupIsReportedFirstWithItsCount()
    {
        var payload = Payload(Call(
            AssistantMcpTools.CountBranches,
            """{"groupBy":"verdict"}"""));

        var first = payload["groups"]!.AsArray()[0]!;
        Assert.Equal(Capture.Branches[1].Recommendation, first["value"]!.GetValue<string>());
        Assert.Equal(2, first["count"]!.GetValue<int>());
    }

    /// <summary>
    /// The surface is the minimum the two supported CLIs use. Anything else is refused by
    /// name rather than quietly answered with something plausible.
    /// </summary>
    [Fact]
    public void AMethodTheBridgeDoesNotServeIsRefusedByName()
    {
        var reply = Dispatch("""{"jsonrpc":"2.0","id":"9","method":"resources/read"}""")!;

        Assert.Equal(MethodNotFound, reply["error"]!["code"]!.GetValue<int>());
        Assert.Equal("9", reply["id"]!.GetValue<string>());
    }

    /// <summary>
    /// A message with no identifier expects no answer at all — not an empty one, which the
    /// client on the other end would try to parse.
    /// </summary>
    [Fact]
    public void ANotificationIsAnsweredWithNothingAtAll()
    {
        Assert.Null(Dispatch("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""));
    }

    private static JsonNode? Dispatch(string message) => AssistantMcpDispatcher.Dispatch(
        Capture,
        JsonSerializer.Deserialize<JsonElement>(message));

    private static JsonNode Call(string tool, string arguments) => Dispatch(
        $$"""
        {
          "jsonrpc":"2.0", "id":3, "method":"tools/call",
          "params":{ "name":"{{tool}}", "arguments":{{arguments}} }
        }
        """)!;

    private static JsonNode Payload(JsonNode reply) => JsonNode.Parse(Text(reply))!;

    private static string Text(JsonNode reply) =>
        reply["result"]!["content"]![0]!["text"]!.GetValue<string>();

    private static bool IsError(JsonNode reply) =>
        reply["result"]!["isError"]!.GetValue<bool>();
}
