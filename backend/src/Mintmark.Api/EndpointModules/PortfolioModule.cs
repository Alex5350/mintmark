using Microsoft.EntityFrameworkCore;
using Mintmark.Api;
using Mintmark.Application.Dtos;
using Mintmark.Domain;
using Mintmark.Domain.Services;
using Mintmark.Domain.ValueObjects;
using Mintmark.Infrastructure;
using Mintmark.Infrastructure.Persistence;
using Npgsql;

namespace Mintmark.Api.EndpointModules;

/// <summary>
/// Portfolio rollup computed in-database: per-metal melt sums via
/// AMW x effective-quantity x latest spot (lateral join on the newest
/// spot row per metal), cost basis over effective revisions, unrealized
/// gain, and allocation weights. Holdings are never loaded into memory —
/// only grouped aggregates cross the wire.
/// </summary>
public sealed class PortfolioModule : IEndpointModule
{
    private sealed record MetalRow(string Metal, decimal TroyOunces, decimal MeltValue, decimal CostBasis);

    private sealed record SeriesRow(long SeriesId, string SeriesName, decimal MeltValue);

    private sealed record TotalsRow(int HoldingCount, decimal CostBasis);

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/portfolio/rollup", async (
            MintmarkDbContext dbContext,
            PriceOptions priceOptions,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var userId = http.RequireUserId();
            var currency = new Currency(priceOptions.BaseCurrency);


            // Effective quantity/price come from the latest revision when one
            // exists (subselects over the append-only revision table); melt
            // joins the newest spot row per metal — all in-database.
            const string effective = """
                WITH eff AS (
                    SELECT h.id,
                           COALESCE((SELECT r.quantity
                                     FROM holding_revisions r
                                     WHERE r.holding_id = h.id
                                     ORDER BY r.revision_number DESC LIMIT 1), h.quantity) AS qty,
                           COALESCE((SELECT r.purchase_price_per_unit_amount
                                     FROM holding_revisions r
                                     WHERE r.holding_id = h.id
                                     ORDER BY r.revision_number DESC LIMIT 1), h.purchase_price_per_unit_amount) AS unit_cost,
                           h.coin_type_id
                    FROM holdings h
                    WHERE h.user_id = @userId AND h.is_deleted = false
                )
                """;

            var byMetal = await dbContext.Database.SqlQueryRaw<MetalRow>($"""
                {effective}
                SELECT s.metal AS "Metal",
                       SUM(eff.qty * ct.actual_metal_weight_troy_oz) AS "TroyOunces",
                       SUM(eff.qty * ct.actual_metal_weight_troy_oz * spot.price) AS "MeltValue",
                       SUM(eff.qty * eff.unit_cost) AS "CostBasis"
                FROM eff
                JOIN coin_types ct ON ct.id = eff.coin_type_id
                JOIN series s ON s.id = ct.series_id
                CROSS JOIN LATERAL (
                    SELECT sp.price_per_troy_ounce_amount AS price
                    FROM spot_prices sp
                    WHERE sp.metal = s.metal AND sp.currency = @currency
                    ORDER BY sp.source_timestamp_utc DESC
                    LIMIT 1
                ) spot
                GROUP BY s.metal
                """, new NpgsqlParameter<long>("userId", userId.Value), new NpgsqlParameter<string>("currency", currency.Code))
                .ToListAsync(cancellationToken);

            var bySeries = await dbContext.Database.SqlQueryRaw<SeriesRow>($"""
                {effective}
                SELECT s.id AS "SeriesId",
                       s.name AS "SeriesName",
                       SUM(eff.qty * ct.actual_metal_weight_troy_oz * spot.price) AS "MeltValue"
                FROM eff
                JOIN coin_types ct ON ct.id = eff.coin_type_id
                JOIN series s ON s.id = ct.series_id
                CROSS JOIN LATERAL (
                    SELECT sp.price_per_troy_ounce_amount AS price
                    FROM spot_prices sp
                    WHERE sp.metal = s.metal AND sp.currency = @currency
                    ORDER BY sp.source_timestamp_utc DESC
                    LIMIT 1
                ) spot
                GROUP BY s.id, s.name
                """, new NpgsqlParameter<long>("userId", userId.Value), new NpgsqlParameter<string>("currency", currency.Code))
                .ToListAsync(cancellationToken);

            // Totals include non-cataloged holdings (cost without melt).
            var totals = await dbContext.Database.SqlQueryRaw<TotalsRow>($"""
                {effective}
                SELECT COUNT(*)::int AS "HoldingCount",
                       COALESCE(SUM(eff.qty * eff.unit_cost), 0) AS "CostBasis"
                FROM eff
                """, new NpgsqlParameter<long>("userId", userId.Value))
                .FirstAsync(cancellationToken);

            var costBasis = new Money(totals.CostBasis, currency);
            var currentValue = byMetal.Count == 0
                ? Money.Zero(currency)
                : new Money(byMetal.Sum(m => m.MeltValue), currency);
            var unrealized = costBasis.IsZero ? 0m : PortfolioMath.UnrealizedGainPercent(currentValue, costBasis);

            var metalValues = byMetal.ToDictionary(m => ParseMetal(m.Metal), m => new Money(m.MeltValue, currency));
            var metalWeights = byMetal.Count == 0
                ? new Dictionary<MetalKind, decimal>()
                : PortfolioMath.AllocateByMetal([.. byMetal.Select(m => new MetalAllocation(ParseMetal(m.Metal), new Money(m.MeltValue, currency)))]);

            var seriesTotal = bySeries.Sum(s => s.MeltValue);
            var bySeriesAllocations = bySeries
                .OrderByDescending(s => s.MeltValue)
                .Take(5)
                .Select(s => new SeriesAllocationDto(
                    new SeriesId(s.SeriesId),
                    s.SeriesName,
                    new Money(s.MeltValue, currency),
                    seriesTotal == 0 ? 0m : Math.Round(s.MeltValue / seriesTotal, 4)))
                .ToList();

            var rollup = new PortfolioRollup(
                totals.HoldingCount,
                costBasis,
                currentValue,
                unrealized,
                [.. metalWeights.Select(a => new MetalAllocationDto(a.Key, metalValues[a.Key], Math.Round(a.Value, 4)))],
                bySeriesAllocations);

            return Results.Ok(rollup);
        })
        .WithTags("Portfolio")
        .RequireAuthorization();
    }

    private static MetalKind ParseMetal(string metal) => Enum.TryParse<MetalKind>(metal, ignoreCase: true, out var parsed)
        ? parsed
        : throw new InvalidOperationException($"Unknown metal '{metal}' in portfolio aggregation.");
}
