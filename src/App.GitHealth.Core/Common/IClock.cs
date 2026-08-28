namespace App.GitHealth.Core.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
