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
                $"A policy accepts at most {MaximumPatternCount} patterns.",
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
                "A branch pattern cannot be empty.",
                parameterName);
        }

        if (pattern.Length > MaximumPatternLength)
        {
            throw new ArgumentException(
                $"A pattern cannot exceed {MaximumPatternLength} characters.",
                parameterName);
        }

        return pattern.Trim();
    }
}
