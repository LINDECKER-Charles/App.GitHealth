namespace App.GitHealth.Core.Branches;

public sealed record BranchPolicy
{
    public const int MaximumPatternCount = 64;
    public const int MaximumPatternLength = 512;

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
        var copy = patterns.Take(MaximumPatternCount + 1).ToArray();
        if (copy.Length > MaximumPatternCount)
        {
            throw new ArgumentException(
                $"Une politique accepte au plus {MaximumPatternCount} motifs.",
                parameterName);
        }

        var normalized = copy
            .Select(pattern => NormalizePattern(pattern, parameterName))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return Array.AsReadOnly(normalized);
    }

    private static string NormalizePattern(string pattern, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException(
                "Un motif de branche ne peut pas être vide.",
                parameterName);
        }

        if (pattern.Length > MaximumPatternLength)
        {
            throw new ArgumentException(
                $"Un motif ne peut pas dépasser {MaximumPatternLength} caractères.",
                parameterName);
        }

        return pattern.Trim();
    }
}
