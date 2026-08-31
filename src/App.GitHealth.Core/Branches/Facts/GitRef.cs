namespace App.GitHealth.Core.Branches;

public sealed record GitRef
{
    public const int MaximumLength = 1024;

    public GitRef(string fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        if (fullName.Length > MaximumLength
            || !TryGetKind(fullName, out var kind)
            || HasInvalidCharacters(fullName))
        {
            throw new ArgumentException(
                "The reference must be a valid local or remote Git branch.",
                nameof(fullName));
        }

        FullName = fullName;
        Kind = kind;
    }

    public string FullName { get; }

    public GitRefKind Kind { get; }

    public string DisplayName => Kind == GitRefKind.LocalBranch
        ? FullName["refs/heads/".Length..]
        : FullName["refs/remotes/".Length..];

    public override string ToString() => FullName;

    private static bool TryGetKind(string fullName, out GitRefKind kind)
    {
        if (fullName.StartsWith("refs/heads/", StringComparison.Ordinal))
        {
            kind = GitRefKind.LocalBranch;
            return fullName.Length > "refs/heads/".Length;
        }

        kind = GitRefKind.RemoteBranch;
        return fullName.StartsWith("refs/remotes/", StringComparison.Ordinal)
            && fullName.Length > "refs/remotes/".Length;
    }

    private static bool HasInvalidCharacters(string fullName)
    {
        var forbidden = " ~^:?*[\\";
        return fullName.Any(character => char.IsControl(character) || forbidden.Contains(character))
            || fullName.Contains("..", StringComparison.Ordinal)
            || fullName.Contains("@{", StringComparison.Ordinal)
            || fullName.Contains("//", StringComparison.Ordinal)
            || fullName.EndsWith('.')
            || fullName.EndsWith('/')
            || fullName.Split('/').Any(component =>
                component.StartsWith('.')
                || component.EndsWith(".lock", StringComparison.OrdinalIgnoreCase));
    }
}
