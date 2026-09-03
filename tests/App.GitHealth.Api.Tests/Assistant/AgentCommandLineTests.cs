using App.GitHealth.Api.Features.Assistant.Agents;

namespace App.GitHealth.Api.Tests.Assistant;

public sealed class AgentCommandLineTests
{
    private const string AnswerFile = "/tmp/githealth-assistant-abc/answer.md";
    private const string BridgeToken = "5f1c0e9a2b7d4c6e8f0a1b2c3d4e5f60";
    private const string BridgeUrl = "http://127.0.0.1:8420/agent-bridge/" + BridgeToken;

    /// <summary>
    /// The guarantee the whole feature rests on. Claude is stripped of every built-in tool
    /// and Codex is held to its own read-only policy; drop either and GitHealth starts
    /// running an agent that can write to the machine.
    /// </summary>
    [Theory]
    [InlineData("claude", "--tools", "")]
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

    /// <summary>
    /// Plan mode withholds edits, but it also refuses the bridge: the run would start and
    /// then have nothing to read. The grant is narrowed with the tool flags instead.
    /// </summary>
    [Fact]
    public void ClaudeIsNotPutInThePlanModeThatWouldRefuseTheBridge()
    {
        Assert.DoesNotContain("--permission-mode", Run("claude").Arguments);
    }

    /// <summary>
    /// Every built-in tool is taken away, then GitHealth's own namespace is granted back, so
    /// the single thing the run can do is read the capture it was started for.
    /// </summary>
    [Fact]
    public void ClaudeIsGrantedBackNothingButTheBridgesOwnTools()
    {
        var arguments = Run("claude").Arguments.ToList();

        Assert.Equal("mcp__githealth", arguments[arguments.IndexOf("--allowedTools") + 1]);
    }

    /// <summary>
    /// The declaration travels inline on the command line rather than through a file, so the
    /// single-use token it carries never lands on disk.
    /// </summary>
    [Fact]
    public void ClaudeIsHandedTheBridgeAsAnInlineServerDeclaration()
    {
        var arguments = Run("claude").Arguments.ToList();

        var declaration = arguments[arguments.IndexOf("--mcp-config") + 1];
        Assert.DoesNotContain(AgentDefinition.BridgeConfigToken, arguments);
        Assert.Contains("\"mcpServers\"", declaration, StringComparison.Ordinal);
        Assert.Contains("\"githealth\"", declaration, StringComparison.Ordinal);
        Assert.Contains($"\"url\":\"{BridgeUrl}\"", declaration, StringComparison.Ordinal);
    }

    /// <summary>
    /// Codex exposes its tool servers only as configuration overrides — the approval mode
    /// included, without which it refuses its own call rather than asking a question no one
    /// is there to answer. The whole table is replaced rather than added to, so the servers
    /// declared on the machine are not carried into the run.
    /// </summary>
    [Fact]
    public void CodexReachesTheBridgeThroughAnOverrideReplacingItsWholeServerTable()
    {
        var arguments = Run("codex").Arguments;

        var servers = Assert.Single(
            arguments,
            argument => argument.StartsWith("mcp_servers=", StringComparison.Ordinal));
        Assert.DoesNotContain(AgentDefinition.BridgeUrlToken, servers, StringComparison.Ordinal);
        Assert.Contains($"url=\"{BridgeUrl}\"", servers, StringComparison.Ordinal);
        Assert.Contains(
            "default_tools_approval_mode=\"approve\"",
            servers,
            StringComparison.Ordinal);
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
        Assert.Equal(
            AnswerFile,
            arguments[arguments.ToList().IndexOf("--output-last-message") + 1]);
    }

    /// <summary>Claude reports its answer in its stream, so it is given no file.</summary>
    [Fact]
    public void AnAgentThatReportsItsAnswerIsGivenNoAnswerFile()
    {
        Assert.DoesNotContain(AnswerFile, Run("claude").Arguments);
    }

    /// <summary>
    /// What makes a run watchable: both CLIs are asked for their JSON stream rather than for
    /// their human log, so the panel can say what the agent is doing while it does it. Drop
    /// either flag and a question goes back to being a spinner.
    /// </summary>
    [Fact]
    public void EveryAgentIsAskedToNarrateWhatItIsDoing()
    {
        var claude = Run("claude").Arguments.ToList();

        Assert.Equal("stream-json", claude[claude.IndexOf("--output-format") + 1]);
        Assert.Contains("--include-partial-messages", claude);
        Assert.Contains("--json", Run("codex").Arguments);
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

    /// <summary>
    /// The shape of the command is what makes the feature auditable and is kept whole. The
    /// token is the one part worth hiding: it is a secret, and the history is exportable.
    /// </summary>
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void TheDescribedCommandBlanksTheRunsBridgeToken(string agentId)
    {
        var command = Run(agentId);

        var described = command.Describe(BridgeToken);
        Assert.Contains(BridgeToken, command.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(BridgeToken, described, StringComparison.Ordinal);
        Assert.Contains("<single-use-token>", described, StringComparison.Ordinal);
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
            BridgeAddress = new Uri(BridgeUrl),
        });

    private static AgentLocation Located(string agentId) => new()
    {
        Agent = AgentCatalog.Find(agentId)!,
        ExecutablePath = $"/usr/local/bin/{agentId}",
        SearchedDirectories = [],
    };
}
