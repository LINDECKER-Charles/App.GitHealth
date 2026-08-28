namespace App.GitHealth.Core.Branches;

public sealed record Contributor
{
    public Contributor(string name, string email, int commitCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentOutOfRangeException.ThrowIfLessThan(commitCount, 1);

        Name = name.Trim();
        Email = email.Trim();
        CommitCount = commitCount;
    }

    public string Name { get; }

    public string Email { get; }

    public int CommitCount { get; }
}
