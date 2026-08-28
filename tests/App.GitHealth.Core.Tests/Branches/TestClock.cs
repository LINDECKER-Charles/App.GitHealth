using App.GitHealth.Core.Common;

namespace App.GitHealth.Core.Tests.Branches;

internal sealed class TestClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
