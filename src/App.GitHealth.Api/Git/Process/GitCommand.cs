namespace App.GitHealth.Api.Git.Process;

internal sealed record GitCommand
{
    private GitCommand(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        string? safeDirectory)
    {
        WorkingDirectory = workingDirectory;
        Arguments = arguments;
        SafeDirectory = safeDirectory;
    }

    public string WorkingDirectory { get; }

    public IReadOnlyList<string> Arguments { get; }

    public string? SafeDirectory { get; }

    public static GitCommand Create(string workingDirectory, IEnumerable<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);
        return Create(workingDirectory, arguments, safeDirectory: null);
    }

    public static GitCommand CreateRepository(
        string repositoryDirectory,
        IEnumerable<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectory);
        return Create(
            Environment.CurrentDirectory,
            arguments,
            Path.GetFullPath(repositoryDirectory));
    }

    private static GitCommand Create(
        string workingDirectory,
        IEnumerable<string> arguments,
        string? safeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);
        return new GitCommand(
            workingDirectory,
            Array.AsReadOnly(arguments.ToArray()),
            safeDirectory);
    }
}
