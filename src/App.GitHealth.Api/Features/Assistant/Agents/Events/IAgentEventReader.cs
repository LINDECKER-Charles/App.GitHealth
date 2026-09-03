using System.Text.Json.Nodes;

namespace App.GitHealth.Api.Features.Assistant.Agents.Events;

/// <summary>
/// Reads one agent's own event format. Each supported CLI streams a different shape of
/// JSON, and this is the single place where that difference is allowed to exist: everything
/// downstream works in <see cref="AgentStep" />.
/// </summary>
internal interface IAgentEventReader
{
    /// <summary>Reads one line. A line this reader does not recognise means nothing.</summary>
    AgentEvent Read(JsonNode line);
}
