using App.GitHealth.Api.Features.Assistant.Agents;

namespace App.GitHealth.Api.Tests.Assistant;

public sealed class AgentCommandLineTests
{
    private const string AnswerFile = "/tmp/githealth-assistant-abc/answer.md";

    /// <summary>
    /// The guarantee the whole feature rests on. If one of these flags is ever dropped,
    /// GitHealth starts running an agent that can write to the machine.
    /// </summary>
    [Theory]
    [InlineData("claude", "--permission-mode", "plan")]
    [InlineData("codex", "--sandbox", "read-only")]
    public void EveryAgentIsAskedForItsReadOnlyMode(
        string agentId,
        string flag,
        string value)
    {
        var arguments = Run(agentId).Arguments;

        var index = arguments.ToList().IndexOf(flag);
        Assert.True(index >= 0, $"{agentId} no longer asks for {flag}.");
        Assert.Equal(value, arguments[index + 1]);
    }

    [Fact]
    public void ClaudeIsRunNonInteractivelyAndWithoutTheMachinesMcpServers()
    {
        var arguments = Run("claude").Arguments;

        Assert.Contains("--print", arguments);
        Assert.Contains("--strict-mcp-config", arguments);
    }

    /// <summary>The scratch directory is not a repository, and Codex checks for one.</summary>
    [Fact]
    public void CodexIsToldItIsRunningOutsideARepository()
    {
        Assert.Contains("--skip-git-repo-check", Run("codex").Arguments);
    }

    [Fact]
    public void TheAnswerFileTokenIsReplacedByTheRunsOwnFile()
    {
        var arguments = Run("codex").Arguments;

        Assert.DoesNotContain(AgentDefinition.AnswerFileToken, arguments);
        Assert.Equal(AnswerFile, arguments[arguments.ToList().IndexOf("--output-last-message") + 1]);
    }

    /// <summary>Claude reports on standard output, so it is given no file to write to.</summary>
    [Fact]
    public void AnAgentThatPrintsItsAnswerIsGivenNoAnswerFile()
    {
        Assert.DoesNotContain(AnswerFile, Run("claude").Arguments);
    }

    [Fact]
    public void TheVersionProbeAsksForNothingElse()
    {
        var command = AgentCommandLine.ForVersion(Located("claude"));

        Assert.Equal(["--version"], command.Arguments);
    }

    [Fact]
    public void TheCommandReadsBackAsItWouldBeTyped()
    {
        var command = AgentCommandLine.ForVersion(Located("codex"));

        Assert.Equal("/usr/local/bin/codex --version", command.ToString());
    }

    [Fact]
    public void AnUnresolvedAgentCannotProduceACommand()
    {
        var location = new AgentLocation
        {
            Agent = AgentCatalog.Find("claude")!,
            SearchedDirectories = [],
        };

        Assert.Throws<InvalidOperationException>(() => AgentCommandLine.ForVersion(location));
    }

    /// <summary>
    /// Codex reads its overrides as options of `exec`, so an effort appended after the `-`
    /// that ends its command line would be silently ignored. The slot fixes the position.
    /// </summary>
    [Theory]
    [InlineData("claude", "--effort", "xhigh")]
    [InlineData("codex", "-c", "model_reasoning_effort=xhigh")]
    public void TheChosenEffortIsPlacedWhereTheAgentExpectsIt(
        string agentId,
        string flag,
        string value)
    {
        var arguments = Run(agentId, AgentEffort.ExtraHigh).Arguments.ToList();

        var index = arguments.IndexOf(flag);
        Assert.True(index >= 0, $"{agentId} no longer carries {flag}.");
        Assert.Equal(value, arguments[index + 1]);
        Assert.DoesNotContain(AgentDefinition.EffortSlot, arguments);
        Assert.DoesNotContain(AgentDefinition.EffortToken, arguments);
    }

    [Fact]
    public void CodexKeepsItsEffortBeforeTheMarkerThatEndsItsCommand()
    {
        var arguments = Run("codex", AgentEffort.Low).Arguments.ToList();

        Assert.True(arguments.IndexOf("-c") < arguments.IndexOf("-"));
    }

    [Fact]
    public void EveryAgentAcceptsTheSameLevels()
    {
        Assert.All(
            AgentCatalog.All,
            agent => Assert.Equal(AgentEffort.All, agent.Efforts));
    }

    /// <summary>
    /// The last gate before a level reaches a command line. Without it a request could write
    /// its own arguments through the effort field.
    /// </summary>
    [Theory]
    [InlineData("ultra")]
    [InlineData("--dangerously-skip-permissions")]
    [InlineData("")]
    public void ALevelOutsideTheAgentsListNeverReachesACommandLine(string effort)
    {
        Assert.Throws<ArgumentException>(() => Run("claude", effort));
    }

    private static AgentCommandLine Run(string agentId, string? effort = null) =>
        AgentCommandLine.ForRun(Located(agentId), new AgentRunOptions
        {
            AnswerFilePath = AnswerFile,
            Effort = effort ?? AgentEffort.Medium,
        });

    private static AgentLocation Located(string agentId) => new()
    {
        Agent = AgentCatalog.Find(agentId)!,
        ExecutablePath = $"/usr/local/bin/{agentId}",
        SearchedDirectories = [],
    };
}
