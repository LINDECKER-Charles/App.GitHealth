namespace App.GitHealth.Api.Persistence.Services;

internal sealed class DatabaseInstanceLease(
    SqliteConnectionFactory connectionFactory) : IDisposable
{
    private readonly Lock _stateLock = new();
    private FileStream? _lockStream;

    public string LockPath => $"{connectionFactory.DatabasePath}.instance.lock";

    public void Acquire()
    {
        lock (_stateLock)
        {
            if (_lockStream is not null)
            {
                return;
            }

            EnsureLockDirectory();
            try
            {
                _lockStream = PrivateFilePermissions.OpenOrCreateExclusive(LockPath);
            }
            catch (IOException exception) when (IsSharingViolation(exception))
            {
                throw new DatabaseInUseException(
                    connectionFactory.DatabasePath,
                    exception);
            }
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            _lockStream?.Dispose();
            _lockStream = null;
        }
    }

    private void EnsureLockDirectory()
    {
        var directory = Path.GetDirectoryName(LockPath)
            ?? throw new InvalidOperationException("The SQLite folder cannot be found.");
        PrivateFilePermissions.EnsureDirectory(directory);
    }

    private static bool IsSharingViolation(IOException exception)
    {
        const int windowsSharingViolation = 32;
        const int windowsLockViolation = 33;
        const int linuxWouldBlock = 11;
        const int macOsWouldBlock = 35;
        var nativeError = exception.HResult & 0xffff;
        return nativeError is windowsSharingViolation or windowsLockViolation
            || (OperatingSystem.IsLinux() && nativeError == linuxWouldBlock)
            || (OperatingSystem.IsMacOS() && nativeError == macOsWouldBlock);
    }
}
