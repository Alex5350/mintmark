using Microsoft.Extensions.DependencyInjection;
using Mintmark.Infrastructure.Prices;
using Quartz;

namespace Mintmark.Infrastructure.Jobs;

/// <summary>
/// Quartz registration for the price jobs.
/// Job store: in-process RAM, deliberately NOT the Postgres ADO store —
/// every unit of durable state these jobs produce (SpotPrice rows,
/// SpotPriceDaily closes) already lives in Postgres, and the schedules are
/// deterministic from configuration and re-registered at each startup, so a
/// crashed process loses nothing but a tick (the cache serves the last row
/// flagged stale until the next fire). Moving to the ADO.NET job store
/// (tables_sql_postgres.sql) is a documented follow-up in
/// docs/open-questions.md.
/// </summary>
public static class QuartzHosting
{
    /// <summary>Registers Quartz with the three price jobs on budget-derived schedules.</summary>
    public static IServiceCollection AddMintmarkQuartz(this IServiceCollection services, PriceOptions priceOptions)
    {
        ArgumentNullException.ThrowIfNull(priceOptions);

        services.AddQuartz(quartz =>
        {
            quartz.SchedulerName = "mintmark";

            quartz.AddJob<SpotPricePollJob>(job => job.WithIdentity(SpotPricePollJob.Key));
            // Budget-derived cadence: full step on weekdays (metals trade
            // ~24/5), half cadence on weekends. Two triggers, one job.
            quartz.AddTrigger(trigger => trigger
                .ForJob(SpotPricePollJob.Key)
                .WithIdentity("spot-poll-weekday")
                .WithCronSchedule(PollSchedule.WeekdayCron(priceOptions.MonthlyBudget)));
            quartz.AddTrigger(trigger => trigger
                .ForJob(SpotPricePollJob.Key)
                .WithIdentity("spot-poll-weekend")
                .WithCronSchedule(PollSchedule.WeekendCron(priceOptions.MonthlyBudget)));

            quartz.AddJob<DailyBackfillJob>(job => job.WithIdentity(DailyBackfillJob.Key));
            quartz.AddTrigger(trigger => trigger
                .ForJob(DailyBackfillJob.Key)
                .WithIdentity("daily-backfill-daily")
                .WithCronSchedule("0 30 2 * * ?")); // 02:30 UTC daily

            quartz.AddJob<RollupJob>(job => job.WithIdentity(RollupJob.Key));
            quartz.AddTrigger(trigger => trigger
                .ForJob(RollupJob.Key)
                .WithIdentity("tick-rollup-nightly")
                .WithCronSchedule("0 0 4 * * ?")); // 04:00 UTC nightly
        });

        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        return services;
    }
}
