using App.GitHealth.Api.Features.Assistant.Agents;
using App.GitHealth.Api.Features.Assistant.Agents.Events;
using App.GitHealth.Api.Features.Assistant.Conversations;
using App.GitHealth.Api.Features.Assistant.Mcp;
using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Persistence.Models.Assistant;
using App.GitHealth.Core.Assistant;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.Assistant;

/// <summary>
/// Starts, reads and stops a conversation with a local agent. The scoped services are all
/// used before the process is launched: what runs in the background holds nothing but the
/// prompt, the command line, the bridge the agent reads the capture through, and the run.
/// </summary>
internal sealed class AssistantRunService(
    AgentAvailabilityService availability,
    AssistantBriefingService briefings,
    AssistantRunRegistry registry,
    AssistantBridge bridge,
    AssistantTurnRecorder journal,
    IOptions<AssistantOptions> options)
{
    public async Task<ApiOutcome<AssistantRunSnapshot>> StartAsync(
        Guid projectId,
        AssistantRunRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var question = AssistantPrompt.NormalizeQuestion(request.Question);
        var agent = Accept(request.AgentId, question);
        if (!agent.IsSuccess)
        {
            return ApiOutcome<AssistantRunSnapshot>.Failed(agent.Failure!);
        }

        var effort = ResolveEffort(agent.Value!, request.Effort);
        if (!effort.IsSuccess)
        {
            return ApiOutcome<AssistantRunSnapshot>.Failed(effort.Failure!);
        }

        var location = await LocateAsync(request.AgentId, cancellationToken);
        if (!location.IsSuccess)
        {
            return ApiOutcome<AssistantRunSnapshot>.Failed(location.Failure!);
        }

        var briefing = await briefings.BuildAsync(projectId, request.Baseline, cancellationToken);
        if (briefing.IsSuccess && briefing.Value!.ConsentGrantedAtUtc is null)
        {
            return ApiOutcome<AssistantRunSnapshot>.Failed(ApiProblems.Forbidden(
                ApiErrorCodes.AssistantConsentRequired,
                "Sending this repository's captures to an agent has not been allowed."));
        }

        return briefing.IsSuccess
            ? Launch(new LaunchRequest(
                projectId,
                location.Value!,
                question,
                effort.Value!,
                briefing.Value!,
                request.ConversationId ?? Guid.NewGuid()))
            : ApiOutcome<AssistantRunSnapshot>.Failed(briefing.Failure!);
    }

    public ApiOutcome<AssistantRunSnapshot> Read(Guid runId, int from)
    {
        var run = registry.Find(runId);
        return run is null
            ? ApiOutcome<AssistantRunSnapshot>.Failed(RunNotFound())
            : ApiOutcome<AssistantRunSnapshot>.Success(run.Read(from));
    }

    public ApiOutcome<AssistantRunSnapshot> Cancel(Guid runId)
    {
        var run = registry.Find(runId);
        if (run is null)
        {
            return ApiOutcome<AssistantRunSnapshot>.Failed(RunNotFound());
        }

        run.RequestCancellation();
        return ApiOutcome<AssistantRunSnapshot>.Success(run.Read(from: 0));
    }

    /// <summary>
    /// What the request asks for, judged against the catalog alone, before the machine is
    /// looked at. "This installation has no such agent" and "this request names an effort
    /// that does not exist" are two different answers, and the second one does not depend on
    /// what happens to be installed: a malformed request is refused the same way everywhere.
    /// </summary>
    private ApiOutcome<AgentDefinition> Accept(string? agentId, string question)
    {
        if (!availability.IsEnabled)
        {
            return ApiOutcome<AgentDefinition>.Failed(ApiProblems.Forbidden(
                ApiErrorCodes.AssistantDisabled,
                "The assistant is disabled on this installation."));
        }

        if (question.Length == 0)
        {
            return ApiOutcome<AgentDefinition>.Failed(ApiProblems.BadRequest(
                ApiErrorCodes.AssistantQuestionRequired,
                "A question is required to start a run."));
        }

        var agent = AgentCatalog.Find(agentId);
        return agent is null
            ? ApiOutcome<AgentDefinition>.Failed(AgentUnavailable(agentId))
            : ApiOutcome<AgentDefinition>.Success(agent);
    }

    /// <summary>Where the accepted agent actually is, or nothing if it is not installed.</summary>
    private async Task<ApiOutcome<AgentLocation>> LocateAsync(
        string? agentId,
        CancellationToken cancellationToken)
    {
        var location = await availability.FindAvailableAsync(agentId, cancellationToken);
        return location is null
            ? ApiOutcome<AgentLocation>.Failed(AgentUnavailable(agentId))
            : ApiOutcome<AgentLocation>.Success(location);
    }

    /// <summary>
    /// An unsupported level is refused rather than quietly downgraded: the panel offers what
    /// the agent declares, so a level outside that list means the caller invented it.
    /// </summary>
    private static ApiOutcome<string> ResolveEffort(AgentDefinition agent, string? requested)
    {
        var effort = AgentEffort.Resolve(requested, agent);
        return AgentEffort.IsSupported(effort, agent)
            ? ApiOutcome<string>.Success(effort)
            : ApiOutcome<string>.Failed(ApiProblems.BadRequest(
                ApiErrorCodes.AssistantEffortUnsupported,
                $"{agent.DisplayName} does not accept the \"{effort}\" effort. "
                + $"Expected one of: {string.Join(", ", agent.Efforts)}."));
    }

    /// <summary>
    /// The bridge is opened before the process is: the agent is handed an address it can
    /// already read, so its first tool call cannot race the session that authorises it.
    /// </summary>
    private ApiOutcome<AssistantRunSnapshot> Launch(LaunchRequest request)
    {
        var key = new AssistantRunKey(Guid.NewGuid(), request.ProjectId);
        var scratch = AssistantScratch.Create();
        AssistantBridgeTicket ticket;
        try
        {
            ticket = bridge.Open(key, request.Capture.Briefing);
        }
        catch (InvalidOperationException exception)
        {
            scratch.Dispose();
            return ApiOutcome<AssistantRunSnapshot>.Failed(ApiProblems.Unavailable(
                ApiErrorCodes.AssistantRunFailed,
                exception.Message));
        }

        return Start(new AgentLaunch
        {
            Key = key,
            Location = request.Location,
            Question = request.Question,
            Effort = request.Effort,
            Capture = request.Capture,
            ConversationId = request.ConversationId,
            AskedAtUtc = DateTimeOffset.UtcNow,
            Scratch = scratch,
            Ticket = ticket,
            CommandLine = AgentCommandLine.ForRun(request.Location, new AgentRunOptions
            {
                AnswerFilePath = scratch.AnswerFilePath,
                Effort = request.Effort,
                BridgeAddress = ticket.Address,
            }),
        });
    }

    private ApiOutcome<AssistantRunSnapshot> Start(AgentLaunch launch)
    {
        var run = new AssistantRun(Describe(launch));
        if (!registry.TryRegister(run))
        {
            run.Dispose();
            launch.Scratch.Dispose();
            bridge.Close(launch.Ticket.Token);
            return ApiOutcome<AssistantRunSnapshot>.Failed(ApiProblems.Conflict(
                ApiErrorCodes.AssistantBusy,
                "Another run is already in progress. Wait for it or stop it first."));
        }

        _ = Task.Run(() => ExecuteAsync(run, launch), CancellationToken.None);
        return ApiOutcome<AssistantRunSnapshot>.Success(run.Read(from: 0));
    }

    private static AssistantRunDescriptor Describe(AgentLaunch launch) => new()
    {
        RunId = launch.Key.RunId,
        ProjectId = launch.Key.ProjectId,
        AgentId = launch.Location.Agent.Id,
        AgentName = launch.Location.Agent.DisplayName,
        Effort = launch.Effort,
        Question = launch.Question,
        CommandLine = launch.CommandLine.Describe(launch.Ticket.Token),
        ConversationId = launch.ConversationId,
        BranchCount = launch.Capture.Briefing.Branches.Count,
        StartedAtUtc = launch.AskedAtUtc,
    };

    private async Task ExecuteAsync(AssistantRun run, AgentLaunch launch)
    {
        // Reports on the calling thread, straight into the run. Progress<T> would post each
        // chunk to the thread pool, where two of them can land out of order — and a run
        // whose steps are shuffled reads worse than one that shows nothing at all.
        var events = AgentEventStream.For(
            launch.Location.Agent,
            run.AppendStep,
            run.AppendTrace);
        try
        {
            var outcome = await AgentProcessRunner.RunAsync(
                CreateRequest(launch),
                events,
                run.CancellationToken);
            Settle(run, ReadAnswer(launch, events, outcome), outcome);
        }
        catch (OperationCanceledException)
        {
            run.MarkCancelled();
        }
        catch (AgentProcessException exception)
        {
            run.Fail(FailureCode(exception.Code), exception.Message);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            run.Fail(ApiErrorCodes.AssistantRunFailed, exception.Message);
        }
        finally
        {
            // The capture stops being readable the moment the agent stops reading it.
            bridge.Close(launch.Ticket.Token);
            launch.Scratch.Dispose();
        }

        await journal.RecordAsync(Transcribe(run, launch));
    }

    /// <summary>
    /// The exchange as it will be kept. Written once the run has settled, whichever way it
    /// settled: a refusal and a stop are part of the history of a repository too.
    /// </summary>
    private static AssistantTurnRecord Transcribe(AssistantRun run, AgentLaunch launch)
    {
        var settled = run.Read(from: 0);
        return new AssistantTurnRecord
        {
            ConversationId = launch.ConversationId,
            AnalysisRunId = launch.Capture.AnalysisId,
            AgentId = settled.AgentId,
            AgentName = settled.AgentName,
            Effort = settled.Effort,
            CommandLine = settled.CommandLine,
            BranchCount = launch.Capture.Briefing.Branches.Count,
            Question = settled.Question,
            AskedAtUtc = settled.StartedAtUtc,
            SettledAtUtc = settled.CompletedAtUtc ?? DateTimeOffset.UtcNow,
            Status = settled.Status,
            Answer = settled.Answer,
            FailureCode = settled.FailureCode,
            FailureMessage = settled.FailureMessage,
            IsTruncated = settled.IsTruncated,
        };
    }

    private AgentRunRequest CreateRequest(AgentLaunch launch) => new()
    {
        CommandLine = launch.CommandLine,
        WorkingDirectory = launch.Scratch.Directory,
        Prompt = AssistantPrompt.Compose(launch.Question),
        Timeout = options.Value.RunTimeout,
        MaximumOutputBytes = options.Value.MaximumOutputBytes,
    };

    private static void Settle(AssistantRun run, string? answer, AgentRunOutcome outcome)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            run.Fail(ApiErrorCodes.AssistantRunFailed, Describe(outcome));
            return;
        }

        run.Complete(answer.Trim(), outcome.IsTruncated);
    }

    /// <summary>
    /// Where the answer is depends on the agent: one reports it in its stream, the other
    /// writes it to the file we named. What the agent wrote on the way there is only read
    /// back when it was stopped short — a run that failed halfway must read as a failure,
    /// not as an answer made of whatever it happened to have said first.
    /// </summary>
    private static string? ReadAnswer(
        AgentLaunch launch,
        AgentEventStream events,
        AgentRunOutcome outcome)
    {
        if (launch.Location.Agent.AnswerSource == AgentAnswerSource.LastMessageFile)
        {
            return launch.Scratch.ReadAnswer();
        }

        var isReadable = outcome.IsSuccess || outcome.IsTruncated;
        return events.Answer ?? (isReadable ? events.Written : null);
    }

    /// <summary>The agent's own words about its failure beat any wording of ours.</summary>
    private static string Describe(AgentRunOutcome outcome)
    {
        var reported = outcome.StandardError.Trim();
        return reported.Length > 0
            ? reported
            : $"The agent exited with code {outcome.ExitCode} without producing an answer.";
    }

    private static string FailureCode(AgentFailureCode code) => code switch
    {
        AgentFailureCode.TimedOut => ApiErrorCodes.AssistantTimedOut,
        AgentFailureCode.Unavailable => ApiErrorCodes.AssistantAgentUnavailable,
        _ => ApiErrorCodes.AssistantRunFailed,
    };

    private static ApiFailure AgentUnavailable(string? agentId)
    {
        var known = AgentCatalog.Find(agentId);
        return ApiProblems.Unavailable(
            ApiErrorCodes.AssistantAgentUnavailable,
            known is null
                ? "No agent of that name is supported."
                : $"{known.DisplayName} is not available on this machine.");
    }

    private static ApiFailure RunNotFound() => ApiProblems.NotFound(
        ApiErrorCodes.AssistantRunNotFound,
        "The requested run does not exist, or it is old enough to have been discarded.");

    private sealed record LaunchRequest(
        Guid ProjectId,
        AgentLocation Location,
        string Question,
        string Effort,
        AssistantCapture Capture,
        Guid ConversationId);

    /// <summary>Everything the background task needs, and nothing that belongs to a request.</summary>
    private sealed record AgentLaunch
    {
        public required AssistantRunKey Key { get; init; }

        public required AgentLocation Location { get; init; }

        public required string Question { get; init; }

        public required string Effort { get; init; }

        public required AssistantCapture Capture { get; init; }

        public required Guid ConversationId { get; init; }

        public required DateTimeOffset AskedAtUtc { get; init; }

        public required AssistantScratch Scratch { get; init; }

        public required AssistantBridgeTicket Ticket { get; init; }

        public required AgentCommandLine CommandLine { get; init; }
    }
}
