using System.Text;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace App.GitHealth.Api.Features.Assistant.Agents;

/// <summary>
/// Runs an agent CLI to completion. The shape mirrors the Git runner — bounded output, a
/// timeout, a killed process tree — because the hazards are the same: a child process that
/// outlives its request, or one that fills memory with its own log.
/// </summary>
internal static class AgentProcessRunner
{
    private const int ReadBufferSize = 2048;

    public static async Task<AgentRunOutcome> RunAsync(
        AgentRunRequest request,
        IProgress<string>? trace,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var process = Start(request);
        using var timeout = new CancellationTokenSource(request.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        try
        {
            return await CommunicateAsync(process, request, trace, linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Kill(process);
            throw new AgentProcessException(
                AgentFailureCode.TimedOut,
                $"{request.CommandLine.Executable} exceeded the allowed "
                + $"{request.Timeout.TotalSeconds:0} seconds and was stopped.");
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }
    }

    private static async Task<AgentRunOutcome> CommunicateAsync(
        DiagnosticsProcess process,
        AgentRunRequest request,
        IProgress<string>? trace,
        CancellationToken cancellationToken)
    {
        // The reads start before the prompt is written: a briefing fills the pipe buffer,
        // and a process blocked on its own output would never drain our stdin.
        var budget = new OutputBudget(request.MaximumOutputBytes);
        var context = new PumpContext(budget, () => Kill(process), trace);
        var output = PumpAsync(process.StandardOutput, context, cancellationToken);
        var error = PumpAsync(process.StandardError, context with { Trace = null }, cancellationToken);
        await WritePromptAsync(process, request.Prompt, cancellationToken);
        var streams = await Task.WhenAll(output, error);
        await process.WaitForExitAsync(cancellationToken);
        return new AgentRunOutcome
        {
            ExitCode = process.ExitCode,
            StandardOutput = streams[0],
            StandardError = streams[1],
            IsTruncated = budget.IsExhausted,
        };
    }

    private static async Task WritePromptAsync(
        DiagnosticsProcess process,
        string prompt,
        CancellationToken cancellationToken)
    {
        try
        {
            await process.StandardInput.WriteAsync(prompt.AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
        }
        catch (IOException)
        {
            // The agent exited before reading its prompt; its own output says why.
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

    /// <summary>
    /// Drains one stream, reporting what arrives so the interface can show a run in
    /// progress rather than a spinner. Exhausting the budget stops the process rather than
    /// only this loop: closing its pipes is what releases the other stream's pending read.
    /// </summary>
    private static async Task<string> PumpAsync(
        StreamReader reader,
        PumpContext context,
        CancellationToken cancellationToken)
    {
        var buffer = new char[ReadBufferSize];
        var content = new StringBuilder();
        while (!context.Budget.IsExhausted)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0)
            {
                return content.ToString();
            }

            var chunk = new string(buffer, 0, context.Budget.Take(count));
            content.Append(chunk);
            context.Trace?.Report(chunk);
        }

        context.OnExhausted();
        return content.ToString();
    }

    private static DiagnosticsProcess Start(AgentRunRequest request)
    {
        var process = new DiagnosticsProcess
        {
            StartInfo = request.CommandLine.CreateStartInfo(request.WorkingDirectory),
        };
        try
        {
            process.Start();
            return process;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
            or InvalidOperationException)
        {
            process.Dispose();
            throw new AgentProcessException(
                AgentFailureCode.Unavailable,
                $"{request.CommandLine.Executable} cannot be started. {exception.Message}");
        }
    }

    private static void Kill(DiagnosticsProcess process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already gone between the check and the call: nothing left to stop.
        }
    }

    private sealed record PumpContext(
        OutputBudget Budget,
        Action OnExhausted,
        IProgress<string>? Trace);

    /// <summary>
    /// Shared between both streams, so a chatty stderr cannot starve the answer. Counted in
    /// characters against a budget expressed in bytes, which under-spends it rather than
    /// over: one character is never less than one byte in UTF-8.
    /// </summary>
    private sealed class OutputBudget(int maximumBytes)
    {
        private int _remaining = maximumBytes;

        public bool IsExhausted => Volatile.Read(ref _remaining) <= 0;

        /// <summary>Consumes up to <paramref name="count" /> and says how much was granted.</summary>
        public int Take(int count)
        {
            var remaining = Interlocked.Add(ref _remaining, -count);
            return remaining >= 0 ? count : Math.Max(0, count + remaining);
        }
    }
}
