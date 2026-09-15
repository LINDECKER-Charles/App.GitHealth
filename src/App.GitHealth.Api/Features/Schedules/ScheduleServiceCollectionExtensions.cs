namespace App.GitHealth.Api.Features.Schedules;

internal static class ScheduleServiceCollectionExtensions
{
    public static IServiceCollection AddSchedules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ScheduleOptions>()
            .Bind(configuration.GetSection(ScheduleOptions.SectionName))
            .Validate(IsTickValid, "Invalid scheduler tick.")
            .Validate(IsTimeZoneValid, "Unknown scheduler time zone.")
            .ValidateOnStart();

        // The zone is read from the system once and held for the process: the screen showing
        // the next firing and the worker acting on it must not resolve it separately.
        services.AddSingleton<ScheduleContext>();
        services.AddSingleton<ScheduledScanRunner>();
        services.AddHostedService<ScheduleWorker>();
        services.AddScoped<ScheduleService>();
        return services;
    }

    private static bool IsTickValid(ScheduleOptions options) =>
        options.TickSeconds is >= ScheduleOptions.MinimumTickSeconds
            and <= ScheduleOptions.MaximumTickSeconds;

    private static bool IsTimeZoneValid(ScheduleOptions options) =>
        ScheduleContext.IsKnownZone(options.TimeZone);
}
