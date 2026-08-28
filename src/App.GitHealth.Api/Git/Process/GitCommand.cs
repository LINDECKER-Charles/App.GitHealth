namespace App.GitHealth.Api.Git.Process;

internal sealed record GitCommand
{
    private GitCommand(string workingDirectory, IReadOnlyList<string> arguments)
    {
        WorkingDirectory = workingDirectory;
        Arguments = arguments;
    }

    public string WorkingDirectory { get; }

    public IReadOnlyList<string> Arguments { get; }

    public static GitCommand Create(string workingDirectory, IEnumerable<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);
        return new GitCommand(workingDirectory, Array.AsReadOnly(arguments.ToArray()));
    }
}
