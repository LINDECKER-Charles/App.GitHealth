namespace App.GitHealth.Core.Branches;

public sealed record BranchPolicy
{
    private BranchPolicy(
        IReadOnlyList<string> excludedPatterns,
        IReadOnlyList<string> protectedPatterns)
    {
        ExcludedPatterns = excludedPatterns;
        ProtectedPatterns = protectedPatterns;
    }

    public static BranchPolicy Empty { get; } = Create([], []);

    public IReadOnlyList<string> ExcludedPatterns { get; }

    public IReadOnlyList<string> ProtectedPatterns { get; }

    public static BranchPolicy Create(
        IEnumerable<string> excludedPatterns,
        IEnumerable<string> protectedPatterns)
    {
        return new BranchPolicy(
            CopyPatterns(excludedPatterns, nameof(excludedPatterns)),
            CopyPatterns(protectedPatterns, nameof(protectedPatterns)));
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<string> CopyPatterns(
        IEnumerable<string> patterns,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(patterns, parameterName);
        var copy = patterns.Select(pattern =>
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException(
                    "Un motif de branche ne peut pas être vide.",
                    parameterName);
            }

            return pattern.Trim();
        }).Distinct(StringComparer.Ordinal).ToArray();

        return Array.AsReadOnly(copy);
    }
}
