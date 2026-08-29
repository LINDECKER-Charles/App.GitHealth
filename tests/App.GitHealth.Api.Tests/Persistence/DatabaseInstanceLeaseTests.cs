using App.GitHealth.Api.Persistence;
using App.GitHealth.Api.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Tests.Persistence;

public sealed class DatabaseInstanceLeaseTests
{
    [Fact]
    public async Task RegisteredHostedServiceHoldsLeaseUntilHostStop()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var lease = database.Services.GetRequiredService<DatabaseInstanceLease>();
        var hostedService = Assert.Single(
            database.Services.GetServices<IHostedService>(),
            service => service is DatabaseMigrationHostedService);

        await hostedService.StartAsync(CancellationToken.None);
        try
        {
            AssertLockIsHeld(lease.LockPath);
            AssertPrivateLock(lease.LockPath);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }

        using var releasedLock = OpenExclusive(lease.LockPath);
    }

    [Fact]
    public async Task HostedServiceAcquiresLeaseBeforeMigration()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var lease = database.Services.GetRequiredService<DatabaseInstanceLease>();
        var migration = new LockObservingMigrationService(lease.LockPath);
        var hostedService = new DatabaseMigrationHostedService(migration, lease);

        await hostedService.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(migration.HasObservedLease);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task SecondInstanceReceivesClearBusinessError()
    {
        await using var firstDatabase = await SqliteTestDatabase.CreateAsync();
        await using var secondDatabase = await firstDatabase.ReopenAsync();
        var firstLease = firstDatabase.Services.GetRequiredService<DatabaseInstanceLease>();
        var secondLease = secondDatabase.Services.GetRequiredService<DatabaseInstanceLease>();
        firstLease.Acquire();

        var exception = Assert.Throws<DatabaseInUseException>(secondLease.Acquire);
        Assert.Equal(firstDatabase.DatabasePath, exception.DatabasePath);
        Assert.Contains(
            "déjà utilisée par une autre instance de GitHealth",
            exception.Message,
            StringComparison.Ordinal);

        firstLease.Dispose();
        secondLease.Acquire();
        secondLease.Dispose();
        Assert.True(File.Exists(firstLease.LockPath));
    }

    [Fact]
    public async Task StaleLockFileDoesNotPreventAcquisition()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var lease = database.Services.GetRequiredService<DatabaseInstanceLease>();
        await File.WriteAllTextAsync(lease.LockPath, "instance interrompue");

        lease.Acquire();
        lease.Dispose();

        Assert.True(File.Exists(lease.LockPath));
    }

    [Fact]
    public void InvalidLockDirectoryIsNotReportedAsInstanceConflict()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "GitHealth-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        var filePath = Path.Combine(rootPath, "not-a-directory");
        File.WriteAllText(filePath, "fichier");
        try
        {
            var factory = new SqliteConnectionFactory(
                Options.Create(new PersistenceOptions
                {
                    DatabasePath = Path.Combine(filePath, "githealth.db"),
                }),
                new TestHostEnvironment(rootPath));
            using var lease = new DatabaseInstanceLease(factory);

            var exception = Assert.Throws<IOException>(lease.Acquire);

            Assert.IsNotType<DatabaseInUseException>(exception);
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    private static void AssertLockIsHeld(string path)
    {
        Assert.Throws<IOException>(() =>
        {
            using var locked = OpenExclusive(path);
        });
    }

    private static FileStream OpenExclusive(string path) => new(
        path,
        FileMode.OpenOrCreate,
        FileAccess.ReadWrite,
        FileShare.None);

    private static void AssertPrivateLock(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            var expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            Assert.Equal(expected, File.GetUnixFileMode(path));
        }
    }

    private sealed class LockObservingMigrationService(string lockPath)
        : IDatabaseMigrationService
    {
        public bool HasObservedLease { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var unexpectedLease = OpenExclusive(lockPath);
            }
            catch (IOException)
            {
                HasObservedLease = true;
                return Task.CompletedTask;
            }

            throw new InvalidOperationException(
                "La migration a démarré avant l’acquisition du verrou SQLite.");
        }
    }
}
