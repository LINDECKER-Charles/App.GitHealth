using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace App.GitHealth.Api.Features.Assistant.Agents.Events;

/// <summary>
/// Reads what an agent says about itself while it runs. Both CLIs are asked for their JSON
/// stream rather than for their human log, so a run reads as a sequence of steps the panel
/// can show instead of as a spinner — and the same stream carries the answer, which no
/// longer has to be scraped out of a log that was never meant to be parsed.
/// </summary>
internal sealed class AgentEventStream : IProgress<string>
{
    private readonly IAgentEventReader _reader;
    private readonly Action<AgentStep> _onStep;
    private readonly Action<string> _onWritten;
    private readonly StringBuilder _pending = new();
    private readonly StringBuilder _written = new();
    private string? _answer;

    private AgentEventStream(
        IAgentEventReader reader,
        Action<AgentStep> onStep,
        Action<string> onWritten)
    {
        _reader = reader;
        _onStep = onStep;
        _onWritten = onWritten;
    }

    /// <summary>The answer the agent reported, while it has reported one.</summary>
    public string? Answer => _answer;

    /// <summary>
    /// Everything the agent wrote, which is what is left to read when it was stopped before
    /// it could report an answer.
    /// </summary>
    public string? Written => _written.Length == 0 ? null : _written.ToString();

    public static AgentEventStream For(
        AgentDefinition agent,
        Action<AgentStep> onStep,
        Action<string> onWritten)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return new AgentEventStream(Reader(agent.Events), onStep, onWritten);
    }

    /// <summary>
    /// Chunks arrive as the pipe fills them, so one read holds a fraction of a line as
    /// readily as several. The line is what carries one event, and the fraction is kept
    /// until the rest of it turns up.
    /// </summary>
    public void Report(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var rest = value.AsSpan();
        int end;
        while ((end = rest.IndexOf('\n')) >= 0)
        {
            _pending.Append(rest[..end]);
            ReadPending();
            rest = rest[(end + 1)..];
        }

        _pending.Append(rest);
    }

    private void ReadPending()
    {
        var line = _pending.ToString().Trim();
        _pending.Clear();
        var node = line.Length == 0 ? null : Parse(line);
        if (node is not null)
        {
            Apply(_reader.Read(node));
        }
    }

    private void Apply(AgentEvent read)
    {
        foreach (var step in read.Steps)
        {
            _onStep(step);
        }

        if (read.Written is { Length: > 0 } written)
        {
            _written.Append(written);
            _onWritten(written);
        }

        if (read.Answer is { Length: > 0 } answer)
        {
            _answer = answer;
        }
    }

    /// <summary>A CLI is free to print a warning of its own; it is simply not an event.</summary>
    private static JsonNode? Parse(string line)
    {
        try
        {
            return JsonNode.Parse(line);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IAgentEventReader Reader(AgentEventFormat format) => format switch
    {
        AgentEventFormat.CodexItems => new CodexEventReader(),
        _ => new ClaudeEventReader(),
    };
}
