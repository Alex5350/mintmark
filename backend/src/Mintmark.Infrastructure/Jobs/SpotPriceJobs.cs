using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Domain.ValueObjects;
using Mintmark.Infrastructure.Persistence;
using Mintmark.Infrastructure.Prices;
using Quartz;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Mintmark.Infrastructure.Jobs;

/// <summary>
/// Polls spot quotes for all four metals and persists them through the
/// PriceCache write path. Cadence is the budget-derived schedule this job is
/// registered with (see <see cref="QuartzHosting"/>); metals.dev's
/// one-call-all-metals shape means a four-metal poll still costs one
/// request. Failures never refire: the next scheduled tick retries, and the
/// cache keeps serving the last row flagged stale.
/// </summary>
public sealed class SpotPricePollJob(
    IServiceScopeFactory scopeFactory,
    ILogger<SpotPricePollJob> logger) : IJob
{
    /// <summary>The Quartz job key.</summary>
    public static readonly JobKey Key = new("spot-price-poll", "prices");

    private static readonly MetalKind[] Metals =
    [
        MetalKind.Gold, MetalKind.Silver, MetalKind.Platinum, MetalKind.Palladium,
    ];

    /// <inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = scopeFactory.CreateScope();
        var composite = scope.ServiceProvider.GetRequiredService<CompositeMetalPriceProvider>();
        var cache = scope.ServiceProvider.GetRequiredService<PriceCache>();
        var options = scope.ServiceProvider.GetRequiredService<PriceOptions>();
        var currency = new Currency(options.BaseCurrency);

        var succeeded = 0;
        foreach (var metal in Metals)
        {
            try
            {
                var quote = await composite.GetCurrentAsync(metal, currency, context.CancellationToken);
                await cache.RecordAsync(quote, metal, cancellationToken: context.CancellationToken);
                succeeded++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Spot poll failed for {Metal}: {Message}", metal, ex.Message);
            }
        }

        if (succeeded == 0)
        {
            logger.LogError("Spot poll: every provider failed; serving last known good rows (stale).");
        }
    }
}

/// <summary>
/// Keeps <see cref="SpotPriceDaily"/> populated: on first run backfills the
/// trailing 30 days (metals.dev timeseries — fixture-tested), afterwards
/// upserts any missing days in the trailing window. Runs daily; one
/// timeseries call per run.
/// </summary>
public sealed class DailyBackfillJob(
    IServiceScopeFactory scopeFactory,
    ILogger<DailyBackfillJob> logger) : IJob
{
    /// <summary>The Quartz job key.</summary>
    public static readonly JobKey Key = new("daily-backfill", "prices");

    /// <summary>The backfill window in days (metals.dev free tier: 30-day window).</summary>
    public const int WindowDays = 30;

    /// <inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MintmarkDbContext>();
        var composite = scope.ServiceProvider.GetRequiredService<CompositeMetalPriceProvider>();
        var options = scope.ServiceProvider.GetRequiredService<PriceOptions>();
        var currency = new Currency(options.BaseCurrency);

        var end = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = end.AddDays(-WindowDays);
        var range = new Application.Ports.DateRange(start, end);

        foreach (var metal in new[] { MetalKind.Gold, MetalKind.Silver })
        {
            try
            {
                var existing = await dbContext.SpotPriceDaily
                    .Where(d => d.Metal == metal && d.Currency == currency && d.Date >= start && d.Date <= end)
                    .Select(d => d.Date)
                    .ToListAsync(context.CancellationToken);

                var known = existing.ToHashSet();
                var quotes = await composite.GetDailyHistoryAsync(metal, currency, range, context.CancellationToken);
                var added = 0;
                foreach (var quote in quotes)
                {
                    if (known.Contains(quote.Date))
                    {
                        continue;
                    }

                    dbContext.SpotPriceDaily.Add(SpotPriceDaily.Create(metal, currency, quote.Date, quote.Close, "backfill"));
                    added++;
                }

                if (added > 0)
                {
                    await dbContext.SaveChangesAsync(context.CancellationToken);
                }

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Daily backfill {Metal}: +{Added} closes", metal, added);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("Daily backfill failed for {Metal}: {Message}", metal, ex.Message);
                }
            }
        }
    }
}

/// <summary>
/// Retention policy (architecture.md): raw ticks older than 90 days roll up
/// into daily closes (per-day average, conflicts with existing closes are
/// kept — first writer wins) and are then deleted, bounding the tick table
/// while daily history is permanent.
/// </summary>
public sealed class RollupJob(
    IServiceScopeFactory scopeFactory,
    ILogger<RollupJob> logger) : IJob
{
    /// <summary>The Quartz job key.</summary>
    public static readonly JobKey Key = new("tick-rollup", "prices");

    /// <summary>Raw ticks older than this are rolled into daily closes.</summary>
    public const int RetentionDays = 90;

    /// <inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MintmarkDbContext>();

        // The 90-day literal is RetentionDays; kept literal because an
        // interpolated hole inside INTERVAL '...' would not parameterize.
        var rolled = await dbContext.Database.ExecuteSqlAsync($"""
            INSERT INTO spot_price_daily (metal, currency, date, close_amount, close_currency, provider_name, ingested_at_utc)
            SELECT metal,
                   currency,
                   (source_timestamp_utc AT TIME ZONE 'UTC')::date,
                   ROUND(AVG(price_per_troy_ounce_amount), 4),
                   MAX(price_per_troy_ounce_currency),
                   MAX(provider_name),
                   now()
            FROM spot_prices
            WHERE source_timestamp_utc < now() - INTERVAL '90 days'
            GROUP BY metal, currency, (source_timestamp_utc AT TIME ZONE 'UTC')::date
            ON CONFLICT DO NOTHING
            """, context.CancellationToken);

        var deleted = await dbContext.Database.ExecuteSqlAsync($"""
            DELETE FROM spot_prices
            WHERE source_timestamp_utc < now() - INTERVAL '90 days'
            """, context.CancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Tick rollup: {Rolled} daily closes inserted, {Deleted} raw ticks deleted", rolled, deleted);
        }
    }
}
