using App.GitHealth.Api.Features.Assistant;
using App.GitHealth.Api.Features.Assistant.Agents;
using App.GitHealth.Api.Features.Assistant.Agents.Events;

namespace App.GitHealth.Api.Tests.Assistant;

/// <summary>
/// A run against a CLI that prints a recording rather than calling a provider. What is
/// proved here is the chain the panel depends on: a real process is launched, its own
/// narration is read off its pipes as it arrives, and the run reads as steps and then as an
/// answer. The readers are covered on their own; this covers them being fed.
/// </summary>
public sealed class AssistantRunNarrationTests : IDisposable
{
    private const string Answer = "Two branches can go: feature/reporting and fix/typo.";

    /// <summary>
    /// What the installed Claude Code prints, trimmed to the fields anything reads. The
    /// first line is not JSON on purpose: a CLI is free to print a notice of its own, and
    /// one must not derail the run that follows it.
    /// </summary>
    private static readonly string Narration = string.Join(
        '\n',
        "fake-agent 1.0",
        """{"type":"system","subtype":"status","status":"requesting"}""",
        """{"type":"stream_event","event":{"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":"","signature":""}}}""",
        """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"toolu_01","name":"mcp__githealth__list_branches","input":{"verdict":"merged","take":50}}]}}""",
        """{"type":"stream_event","event":{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}}""",
        """{"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Two branches "}}}""",
        $$"""{"type":"result","subtype":"success","is_error":false,"result":"{{Answer}}"}""",
        string.Empty);

    private readonly FakeAgent _agent = FakeAgent.Printing(Narration);

    [Fact]
    public async Task ARunNarratesItselfWhileItHappensAndThenAnswers()
    {
        var run = NewRun();
        var events = AgentEventStream.For(
            AgentCatalog.Find("claude")!,
            run.AppendStep,
            run.AppendTrace);

        var outcome = await AgentProcessRunner.RunAsync(
            _agent.Request,
            events,
            CancellationToken.None);

        var settled = run.Read(from: 0);
        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(Answer, events.Answer);
        Assert.Equal(
            ["Waiting", "Thinking", "Tool", "Writing"],
            settled.Steps.Select(step => step.Kind));
        var call = Assert.Single(settled.Steps, step => step.Kind == "Tool");
        Assert.Equal("list_branches", call.Label);
        Assert.Equal("verdict=merged, take=50", call.Detail);
    }

    /// <summary>
    /// The answer is written as it is written, so a run stopped halfway still has something
    /// to show rather than only the last thing it happened to do.
    /// </summary>
    [Fact]
    public async Task WhatTheAgentWritesIsReadableBeforeItHasFinishedWriting()
    {
        var run = NewRun();
        var events = AgentEventStream.For(
            AgentCatalog.Find("claude")!,
            run.AppendStep,
            run.AppendTrace);

        await AgentProcessRunner.RunAsync(_agent.Request, events, CancellationToken.None);

        Assert.Equal("Two branches ", run.Read(from: 0).Trace);
    }

    /// <summary>
    /// An agent that asks the model five times in a row is doing one thing. A list that
    /// repeated it five times would say less than one that says it once.
    /// </summary>
    [Fact]
    public void TheSameActivityTwiceRunningIsKeptOnce()
    {
        var run = NewRun();

        run.AppendStep(new AgentStep(AgentStepKind.Waiting));
        run.AppendStep(new AgentStep(AgentStepKind.Waiting));
        run.AppendStep(new AgentStep(AgentStepKind.Tool, "list_branches", "take=50"));
        run.AppendStep(new AgentStep(AgentStepKind.Tool, "list_branches", "take=10"));

        Assert.Equal(
            ["Waiting", "Tool", "Tool"],
            run.Read(from: 0).Steps.Select(step => step.Kind));
    }

    public void Dispose() => _agent.Dispose();

    private static AssistantRun NewRun() => new(new AssistantRunDescriptor
    {
        RunId = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        AgentId = "claude",
        AgentName = "Claude Code",
        Effort = "low",
        Question = "Which branches can I clean up?",
        CommandLine = "claude --print",
        ConversationId = Guid.NewGuid(),
        BranchCount = 12,
        StartedAtUtc = DateTimeOffset.UtcNow,
    });

    /// <summary>
    /// A CLI that prints a recording and never reads its prompt, which is also worth
    /// covering: a process that closes its input mid-write must not fail the run.
    /// </summary>
    private sealed class FakeAgent : IDisposable
    {
        private readonly string _directory;

        private FakeAgent(string directory, AgentRunRequest request)
        {
            _directory = directory;
            Request = request;
        }

        public AgentRunRequest Request { get; }

        public static FakeAgent Printing(string output)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                $"githealth-fake-agent-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var recording = Path.Combine(directory, "recording.jsonl");
            File.WriteAllText(recording, output);
            return new FakeAgent(directory, new AgentRunRequest
            {
                CommandLine = Command(directory, recording),
                WorkingDirectory = directory,
                Prompt = "Which branches can I clean up?",
                Timeout = TimeSpan.FromSeconds(30),
                MaximumOutputBytes = 64 * 1024,
            });
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private static AgentCommandLine Command(string directory, string recording)
        {
            if (OperatingSystem.IsWindows())
            {
                return new AgentCommandLine
                {
                    Executable = "cmd.exe",
                    Arguments = ["/c", "type", recording],
                };
            }

            var script = Path.Combine(directory, "agent.sh");
            File.WriteAllText(script, $"#!/bin/sh\nexec cat \"{recording}\"\n");
            File.SetUnixFileMode(
                script,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return new AgentCommandLine { Executable = script, Arguments = [] };
        }
    }
}
