using App.GitHealth.Core.Common;

namespace App.GitHealth.Core.Tests.Branches;

// Shared deterministic clock for branch classification scenarios.
internal sealed class TestClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
