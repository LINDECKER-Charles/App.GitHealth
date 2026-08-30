using App.GitHealth.Api.Git.Process;

namespace App.GitHealth.Api.Git;

internal sealed class GitRuntimeDiagnostic
{
    private readonly object _sync = new();
    private readonly GitExecutableResolver _resolver;
    private bool _isAvailable;
    private string _message = "Détection de Git en attente.";

    public GitRuntimeDiagnostic(GitExecutableResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
    }

    /// <summary>
    /// Chemin de l'exécutable retenu, ou <see langword="null" /> s'il reste introuvable.
    /// </summary>
    public string? ExecutablePath => _resolver.Location.ExecutablePath;

    public (bool IsAvailable, string Message) Read()
    {
        lock (_sync)
        {
            return (_isAvailable, _message);
        }
    }

    public void ReportAvailable(string version)
    {
        lock (_sync)
        {
            _isAvailable = true;
            _message = version;
        }
    }

    public void ReportUnavailable(string message)
    {
        lock (_sync)
        {
            _isAvailable = false;
            _message = message;
        }
    }
}
