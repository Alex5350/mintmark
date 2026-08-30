using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Mintmark.Application.Ports;
using Mintmark.Infrastructure.Persistence;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Infrastructure.Prices;

/// <summary>Thrown when no spot row exists at all (fresh database, poller never ran).</summary>
public sealed class PriceUnavailableException(string message) : Exception(message);

/// <summary>
/// The two-tier price cache (architecture.md): an IMemoryCache tier with a
/// TTL derived from the budget math over the authoritative Postgres
/// <c>spot_prices</c> table, read-through on every request. Only the poller
/// writes tiers (via <see cref="RecordAsync"/>); a provider outage therefore
/// serves the last known good row <c>Stale</c> — never silently fresh. This
/// class is registered as the request-path <see cref="IMetalPriceProvider"/>.
/// </summary>
public sealed class PriceCache(
    MintmarkDbContext dbContext,
    IMemoryCache memory,
    PriceOptions options,
    TimeProvider? timeProvider = null) : IMetalPriceProvider
{
    /// <summary>The staleness verdict of a cache read.</summary>
    /// <param name="Quote">The authoritative quote (last success wins).</param>
    /// <param name="Stale">True when older than the freshness window.</param>
    /// <param name="StaleSince">When the quote was observed; set exactly when <paramref name="Stale"/> is true.</param>
    public sealed record PriceQuoteResult(ProviderQuote Quote, bool Stale, DateTimeOffset? StaleSince);

    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    private TimeSpan MemoryTtl => PollSchedule.MemoryCacheTtl(options.MonthlyBudget);

    private TimeSpan FreshWindow => PollSchedule.FreshnessWindow(options.MonthlyBudget);

    /// <summary>
    /// Reads the current quote through both tiers. Optionally stores the
    /// backing <see cref="SpotPriceId"/> for valuation provenance.
    /// </summary>
    /// <exception cref="PriceUnavailableException">Thrown when no row exists for the metal/currency.</exception>
    public async Task<PriceQuoteResult> GetAsync(
        MetalKind metal,
        Currency currency,
        CancellationToken cancellationToken = default)
    {
        var key = CacheKey(metal, currency);
        if (memory.TryGetValue(key, out PriceQuoteResult? hit) && hit is not null)
        {
            return hit;
        }

        var row = await dbContext.SpotPrices
            .Where(p => p.Metal == metal && p.Currency == currency)
            .OrderByDescending(p => p.SourceTimestampUtc)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new PriceUnavailableException(
                $"No spot price recorded yet for {metal} {currency}; the poller has not run.");

        var result = Evaluate(row);
        Store(key, result);
        return result;
    }

    /// <summary>Loads the latest persisted row ids for provenance (valuation FK).</summary>
    public Task<Dictionary<MetalKind, SpotPriceId>> LatestRowIdsAsync(Currency currency, CancellationToken cancellationToken = default) =>
        dbContext.SpotPrices
            .Where(p => p.Currency == currency)
            .GroupBy(p => p.Metal)
            .Select(g => new { Metal = g.Key, Id = g.OrderByDescending(p => p.SourceTimestampUtc).Select(p => p.Id).First() })
            .ToDictionaryAsync(x => x.Metal, x => x.Id, cancellationToken);

    /// <summary>
    /// Poller write path: persists the quote as an authoritative
    /// <see cref="SpotPrice"/> row and refreshes the memory tier.
    /// </summary>
    public async Task RecordAsync(
        ProviderQuote quote,
        MetalKind metal,
        DateTimeOffset? sourceTimestampUtc = null,
        CancellationToken cancellationToken = default)
    {
        var currency = quote.Price.Currency;
        var row = SpotPrice.Create(
            metal,
            quote.Price,
            quote.Bid,
            quote.Ask,
            quote.ProviderName,
            sourceTimestampUtc ?? quote.SourceTimestampUtc);

        dbContext.SpotPrices.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        Store(CacheKey(metal, currency), Evaluate(row));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Request-path surface for services bound to <see cref="IMetalPriceProvider"/>
    /// (valuations): serves the cached tier and never touches the network.
    /// </remarks>
    public async Task<ProviderQuote> GetCurrentAsync(MetalKind metal, Currency currency, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync(metal, currency, cancellationToken);
        return result.Quote;
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always thrown: history reads go straight to Postgres (SpotPriceDaily), not providers.</exception>
    public Task<IReadOnlyList<DailySpotQuote>> GetDailyHistoryAsync(
        MetalKind metal,
        Currency currency,
        DateRange range,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("History is served from spot_price_daily; the poller backfills it.");

    private PriceQuoteResult Evaluate(SpotPrice row)
    {
        var age = _clock.GetUtcNow() - row.SourceTimestampUtc;
        var stale = age > FreshWindow;
        var currency = row.Currency;
        var quote = new ProviderQuote(
            row.PricePerTroyOunce,
            row.BidPerTroyOunce,
            row.AskPerTroyOunce,
            row.ProviderName,
            row.SourceTimestampUtc);

        return new PriceQuoteResult(quote, stale, stale ? row.SourceTimestampUtc : null);
    }

    private void Store(string key, PriceQuoteResult result)
    {
        // Stale quotes re-validate quickly (a poller success must show fast);
        // fresh quotes sit for the budget-derived TTL.
        var ttl = result.Stale ? TimeSpan.FromMinutes(1) : MemoryTtl;
        _ = memory.Set(key, result, ttl);
    }

    private static string CacheKey(MetalKind metal, Currency currency) => $"spot:{metal}:{currency.Code}";
}
