using System.Text.Json;
using App.GitHealth.Api.Persistence;
using App.GitHealth.Api.Persistence.Services;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.LocalApi;

/// <summary>
/// Keeps the local API's own settings in a private file next to the database rather than in
/// it. Two reasons, both deliberate: a database export must not carry the fingerprint of a
/// live token, and a switch that decides whether a port is opened at boot has to be readable
/// before the first migration runs.
/// </summary>
internal sealed partial class LocalApiSettingsStore
{
    private const string FileName = "local-api.json";
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly ILogger<LocalApiSettingsStore> _logger;
    private readonly Lock _gate = new();
    private readonly string _path;
    private LocalApiSettings? _cached;

    public LocalApiSettingsStore(
        IOptions<PersistenceOptions> persistence,
        ILogger<LocalApiSettingsStore> logger)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        _logger = logger;
        _path = Path.Combine(DirectoryOf(persistence.Value.DatabasePath), FileName);
    }

    public LocalApiSettings Read()
    {
        lock (_gate)
        {
            return _cached ??= ReadFile();
        }
    }

    public void Write(LocalApiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_gate)
        {
            PrivateFilePermissions.EnsureDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, SerializerOptions));
            PrivateFilePermissions.EnsureFile(_path);
            _cached = settings;
        }
    }

    /// <summary>
    /// An unreadable file reads as "closed". Opening a port on a guess would be the wrong
    /// way round: the user asked for the settings they wrote, and those are gone.
    /// </summary>
    private LocalApiSettings ReadFile()
    {
        if (!File.Exists(_path))
        {
            return LocalApiSettings.Closed;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<LocalApiSettings>(
                File.ReadAllText(_path),
                SerializerOptions);
            return settings ?? LocalApiSettings.Closed;
        }
        catch (Exception exception) when (exception is JsonException or IOException
            or UnauthorizedAccessException)
        {
            LogUnreadable(_logger, _path, exception);
            return LocalApiSettings.Closed;
        }
    }

    private static string DirectoryOf(string databasePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        return string.IsNullOrEmpty(directory) ? Directory.GetCurrentDirectory() : directory;
    }

    [LoggerMessage(
        EventId = 1203,
        Level = LogLevel.Warning,
        Message = "The local API settings at {Path} could not be read; the access stays closed.")]
    private static partial void LogUnreadable(
        ILogger logger,
        string path,
        Exception exception);
}
