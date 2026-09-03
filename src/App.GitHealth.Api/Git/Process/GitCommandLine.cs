namespace App.GitHealth.Api.Git.Process;

/// <summary>
/// Renders a command the way a reader would type it. The hardening flags GitHealth injects
/// and the repository path it targets are left out: both are constant and already known.
/// </summary>
internal static class GitCommandLine
{
    private const int MaximumCommandLength = 220;
    private const int MaximumOutputLength = 120;
    private const string Ellipsis = "…";
    private const string RepositoryOption = "-C";

    public static string Describe(GitCommand command)
    {
        var arguments = command.Arguments;
        var start = arguments.Count >= 2 && arguments[0] == RepositoryOption ? 2 : 0;
        var rendered = string.Join(' ', arguments.Skip(start).Select(Quote));
        return Shorten($"git {rendered}".TrimEnd(), MaximumCommandLength);
    }

    /// <summary>What Git answered, reduced to its first line: the console shows one line.</summary>
    public static string? SummariseOutput(string output)
    {
        var line = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
        return line is null ? null : Shorten(line.Replace('\t', ' ').Trim(), MaximumOutputLength);
    }

    private static string Quote(string argument) =>
        argument.Contains(' ', StringComparison.Ordinal) ? $"'{argument}'" : argument;

    private static string Shorten(string value, int maximumLength) =>
        value.Length <= maximumLength
            ? value
            : string.Concat(value.AsSpan(0, maximumLength - 1), Ellipsis);
}
