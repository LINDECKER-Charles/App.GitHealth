using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Api.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.GitHealth.Api.Persistence;

internal static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                Microsoft.Extensions.Options.IValidateOptions<PersistenceOptions>,
                PersistenceOptionsValidator>());
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddDbContextFactory<GitHealthDbContext>(ConfigureContext);
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IAnalysisRepository, AnalysisRepository>();

        // A conversation is written when the agent stops, on a task that outlives the
        // request that started it, so this one repository cannot be scoped to a request.
        services.AddSingleton<
            IAssistantConversationRepository,
            AssistantConversationRepository>();
        services.AddScoped<IRetentionService, RetentionService>();
        services.AddSingleton<DatabaseInstanceLease>();
        services.AddSingleton<IDatabaseMigrationService, DatabaseMigrationService>();
        services.AddSingleton<IDatabaseBackupService, SqliteDatabaseBackupService>();
        services.AddHostedService<DatabaseMigrationHostedService>();
        return services;
    }

    private static void ConfigureContext(
        IServiceProvider provider,
        DbContextOptionsBuilder optionsBuilder)
    {
        var connection = provider.GetRequiredService<SqliteConnectionFactory>();
        optionsBuilder.UseSqlite(connection.ConnectionString, sqlite =>
        {
            sqlite.CommandTimeout(connection.WriteTimeoutSeconds);
            sqlite.MigrationsAssembly(typeof(GitHealthDbContext).Assembly.FullName);
        });
    }
}
