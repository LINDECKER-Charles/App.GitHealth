using App.GitHealth.Api.Features.Assistant.Mcp;

namespace App.GitHealth.Api.Tests.Assistant;

public sealed class AssistantMcpSessionRegistryTests
{
    /// <summary>
    /// A capture is reachable only while the run started to read it is still going. Closing
    /// is what stops the bridge from being a standing read of the database.
    /// </summary>
    [Fact]
    public void AnOpenTokenReachesItsCaptureAndReachesNothingOnceItIsClosed()
    {
        var registry = new AssistantMcpSessionRegistry();
        var runId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var capture = AssistantTestCapture.Create();

        var session = registry.Open(runId, projectId, capture);

        var found = registry.Find(session.Token);
        Assert.Equal(runId, found!.RunId);
        Assert.Equal(projectId, found.ProjectId);
        Assert.Same(capture, found.Capture);
        registry.Close(session.Token);
        Assert.Null(registry.Find(session.Token));
    }

    /// <summary>
    /// One run, one token. Two runs sharing one would let either read the other's capture,
    /// which is the whole thing the token is there to prevent.
    /// </summary>
    [Fact]
    public void TwoRunsNeverShareAToken()
    {
        var registry = new AssistantMcpSessionRegistry();

        var first = registry.Open(Guid.NewGuid(), Guid.NewGuid(), AssistantTestCapture.Create());
        var second = registry.Open(Guid.NewGuid(), Guid.NewGuid(), AssistantTestCapture.Create());

        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal(first.RunId, registry.Find(first.Token)!.RunId);
        Assert.Equal(second.RunId, registry.Find(second.Token)!.RunId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-token-never-issued")]
    public void ATokenTheRegistryNeverHandedOutFindsNothing(string? token)
    {
        Assert.Null(new AssistantMcpSessionRegistry().Find(token));
    }
}
