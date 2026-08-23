namespace Mintmark.Infrastructure.Prices;

/// <summary>
/// Poll cadence derived from the configured monthly request budget
/// (architecture.md: "interval computed from the configured monthly budget
/// with headroom, and slows outside metals market hours"). Metals trade
/// ~24/5 (Sun evening – Fri evening ET) so weekdays run at the full cadence
/// and weekends at half of it. Both cron schedules below express the same
/// hour step.
/// </summary>
public static class PollSchedule
{
    /// <summary>
    /// Computes the weekday poll interval. Formula: hourly step =
    /// ceiling(24 × 30 / monthly budget) so a 250/month budget yields a 3-hour
    /// step ≈ 208 polls/month — inside the budget with headroom (a 2-hour
    /// step would exceed it once weekend polls are counted).
    /// </summary>
    public static TimeSpan WeekdayInterval(int monthlyBudget)
    {
        if (monthlyBudget < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyBudget), monthlyBudget, "Monthly budget must be at least 1.");
        }

        var hours = Math.Ceiling(24.0 * 30.0 / monthlyBudget);
        return TimeSpan.FromHours(Math.Clamp(hours, 1.0, 24.0));
    }

    /// <summary>Gets the weekend poll interval (half the weekday cadence — markets closed).</summary>
    public static TimeSpan WeekendInterval(int monthlyBudget) => WeekdayInterval(monthlyBudget) * 2;

    /// <summary>Gets the in-memory cache TTL: a quarter of the poll interval, clamped to [1 min, 15 min].</summary>
    public static TimeSpan MemoryCacheTtl(int monthlyBudget) =>
        TimeSpan.FromTicks(Math.Clamp(
            WeekdayInterval(monthlyBudget).Ticks / 4,
            TimeSpan.FromMinutes(1).Ticks,
            TimeSpan.FromMinutes(15).Ticks));

    /// <summary>
    /// Gets the freshness window after which a quote is flagged stale:
    /// three weekday poll intervals (a provider outage outlives one missed
    /// poll without panicking clients).
    /// </summary>
    public static TimeSpan FreshnessWindow(int monthlyBudget) => WeekdayInterval(monthlyBudget) * 3;

    /// <summary>Builds the Quartz cron expression for the weekday cadence (every N hours on Mon–Fri).</summary>
    public static string WeekdayCron(int monthlyBudget)
    {
        var hours = (int)WeekdayInterval(monthlyBudget).TotalHours;
        return $"0 0 */{hours} ? * MON-FRI";
    }

    /// <summary>Builds the Quartz cron expression for the reduced weekend cadence (every 2N hours on Sat/Sun).</summary>
    public static string WeekendCron(int monthlyBudget)
    {
        var hours = (int)WeekendInterval(monthlyBudget).TotalHours;
        return $"0 0 */{hours} ? * SAT-SUN";
    }
}
