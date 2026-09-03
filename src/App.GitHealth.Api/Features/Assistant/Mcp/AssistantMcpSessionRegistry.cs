using System.Security.Cryptography;
using App.GitHealth.Core.Assistant;

namespace App.GitHealth.Api.Features.Assistant.Mcp;

/// <summary>
/// Hands out the tokens the bridge accepts, one per run, and takes them back the moment the
/// run settles. A capture is reachable only while the agent that was started to read it is
/// still running, which is what stops the bridge being a standing read of the database.
/// </summary>
internal sealed class AssistantMcpSessionRegistry
{
    /// <summary>256 bits, so a token is not worth guessing even from the loopback side.</summary>
    private const int TokenBytes = 32;

    /// <summary>
    /// Backstop for a run that never settles. It outlives the longest allowed run so that a
    /// legitimate agent is never cut off mid-answer by this clock rather than by its own.
    /// </summary>
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(20);

    private readonly Lock _sync = new();
    private readonly Dictionary<string, AssistantMcpSession> _sessions =
        new(StringComparer.Ordinal);

    public AssistantMcpSession Open(Guid runId, Guid projectId, AnalysisBriefing capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var session = new AssistantMcpSession
        {
            Token = RandomNumberGenerator.GetHexString(TokenBytes * 2, lowercase: true),
            RunId = runId,
            ProjectId = projectId,
            Capture = capture,
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(Grace),
        };
        using (_sync.EnterScope())
        {
            PruneExpired();
            _sessions.Add(session.Token, session);
        }

        return session;
    }

    /// <summary>Null for an unknown, closed or expired token — the caller cannot tell which.</summary>
    public AssistantMcpSession? Find(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        using (_sync.EnterScope())
        {
            if (!_sessions.TryGetValue(token, out var session))
            {
                return null;
            }

            if (session.ExpiresAtUtc > DateTimeOffset.UtcNow)
            {
                return session;
            }

            _sessions.Remove(token);
            return null;
        }
    }

    public void Close(string token)
    {
        using (_sync.EnterScope())
        {
            _sessions.Remove(token);
        }
    }

    private void PruneExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (token, session) in _sessions)
        {
            if (session.ExpiresAtUtc <= now)
            {
                _sessions.Remove(token);
            }
        }
    }
}
