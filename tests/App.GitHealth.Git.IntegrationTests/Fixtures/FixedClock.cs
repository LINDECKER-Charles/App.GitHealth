using App.GitHealth.Core.Common;

namespace App.GitHealth.Git.IntegrationTests.Fixtures;

internal sealed class FixedClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
