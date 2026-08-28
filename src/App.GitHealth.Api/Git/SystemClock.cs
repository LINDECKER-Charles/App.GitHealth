using App.GitHealth.Core.Common;

namespace App.GitHealth.Api.Git;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
