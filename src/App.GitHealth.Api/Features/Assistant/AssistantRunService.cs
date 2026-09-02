using App.GitHealth.Api.Features.Assistant.Agents;
using App.GitHealth.Api.Features.Common;
using App.GitHealth.Core.Assistant;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.Assistant;

/// <summary>
/// Starts, reads and stops a conversation with a local agent. The scoped services are all
/// used before the process is launched: what runs in the background holds nothing but the
/// prompt, the command line and the run itself.
/// </summary>
internal sealed class AssistantRunService(
    AgentAvailabilityService availability,
    AssistantBriefingService briefings,
    AssistantRunRegistry registry,
    IOptions<AssistantOptions> options)
{
    public async Task<ApiOutcome<AssistantRunSnapshot>> StartAsync(
        Guid projectId,
        AssistantRunRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var question = AssistantPrompt.NormalizeQuestion(request.Question);
        var agent = await ResolveAsync(request.AgentId, question, cancellationToken);
        if (!agent.IsSuccess)
        {
            return ApiOutcome<AssistantRunSnapshot>.Failed(agent.Failure!);
        }

        var briefing = await briefings.BuildAsync(projectId, request.Baseline, cancellationToken);
        return briefing.IsSuccess
            ? Launch(projectId, agent.Value!, AssistantPrompt.Compose(briefing.Value!, question), question)
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

    private async Task<ApiOutcome<AgentLocation>> ResolveAsync(
        string? agentId,
        string question,
        CancellationToken cancellationToken)
    {
        if (!availability.IsEnabled)
        {
            return ApiOutcome<AgentLocation>.Failed(ApiProblems.Forbidden(
                ApiErrorCodes.AssistantDisabled,
                "The assistant is disabled on this installation."));
        }

        if (question.Length == 0)
        {
            return ApiOutcome<AgentLocation>.Failed(ApiProblems.BadRequest(
                ApiErrorCodes.AssistantQuestionRequired,
                "A question is required to start a run."));
        }

        var location = await availability.FindAvailableAsync(agentId, cancellationToken);
        return location is null
            ? ApiOutcome<AgentLocation>.Failed(AgentUnavailable(agentId))
            : ApiOutcome<AgentLocation>.Success(location);
    }

    private ApiOutcome<AssistantRunSnapshot> Launch(
        Guid projectId,
        AgentLocation location,
        string prompt,
        string question)
    {
        var scratch = AssistantScratch.Create();
        var commandLine = AgentCommandLine.ForRun(location, scratch.AnswerFilePath);
        var run = new AssistantRun(Describe(projectId, location, question, commandLine));
        if (!registry.TryRegister(run))
        {
            run.Dispose();
            scratch.Dispose();
            return ApiOutcome<AssistantRunSnapshot>.Failed(ApiProblems.Conflict(
                ApiErrorCodes.AssistantBusy,
                "Another run is already in progress. Wait for it or stop it first."));
        }

        var launch = new AgentLaunch(location, commandLine, prompt, scratch);
        _ = Task.Run(() => ExecuteAsync(run, launch), CancellationToken.None);
        return ApiOutcome<AssistantRunSnapshot>.Success(run.Read(from: 0));
    }

    private static AssistantRunDescriptor Describe(
        Guid projectId,
        AgentLocation location,
        string question,
        AgentCommandLine commandLine) => new()
        {
            RunId = Guid.NewGuid(),
            ProjectId = projectId,
            AgentId = location.Agent.Id,
            AgentName = location.Agent.DisplayName,
            Question = question,
            CommandLine = commandLine.ToString(),
            StartedAtUtc = DateTimeOffset.UtcNow,
        };

    private async Task ExecuteAsync(AssistantRun run, AgentLaunch launch)
    {
        try
        {
            var outcome = await AgentProcessRunner.RunAsync(
                CreateRequest(launch),
                new TraceSink(run),
                run.CancellationToken);
            Settle(run, launch, outcome);
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
            launch.Scratch.Dispose();
        }
    }

    private AgentRunRequest CreateRequest(AgentLaunch launch) => new()
    {
        CommandLine = launch.CommandLine,
        WorkingDirectory = launch.Scratch.Directory,
        Prompt = launch.Prompt,
        Timeout = options.Value.RunTimeout,
        MaximumOutputBytes = options.Value.MaximumOutputBytes,
    };

    private static void Settle(AssistantRun run, AgentLaunch launch, AgentRunOutcome outcome)
    {
        var answer = ReadAnswer(launch, outcome);
        if (answer is null)
        {
            run.Fail(ApiErrorCodes.AssistantRunFailed, Describe(outcome));
            return;
        }

        run.Complete(answer, outcome.IsTruncated);
    }

    /// <summary>
    /// Where the answer is depends on the agent: one prints it, the other writes it to the
    /// file we named and keeps its standard output for its own log.
    /// </summary>
    private static string? ReadAnswer(AgentLaunch launch, AgentRunOutcome outcome)
    {
        if (launch.Location.Agent.AnswerSource == AgentAnswerSource.LastMessageFile)
        {
            return launch.Scratch.ReadAnswer();
        }

        var printed = outcome.StandardOutput.Trim();
        var isReadable = outcome.IsSuccess || outcome.IsTruncated;
        return isReadable && printed.Length > 0 ? printed : null;
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

    private sealed record AgentLaunch(
        AgentLocation Location,
        AgentCommandLine CommandLine,
        string Prompt,
        AssistantScratch Scratch);

    /// <summary>
    /// Reports on the calling thread. <see cref="Progress{T}" /> would post each chunk to
    /// the thread pool, where two of them can land out of order — and a trace whose lines
    /// are shuffled is worse than no trace.
    /// </summary>
    private sealed record TraceSink(AssistantRun Run) : IProgress<string>
    {
        public void Report(string value) => Run.AppendTrace(value);
    }
}
