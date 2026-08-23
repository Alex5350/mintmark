using Microsoft.EntityFrameworkCore;
using Mintmark.Api;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Domain.Services;
using Mintmark.Domain.ValueObjects;
using Mintmark.Infrastructure;
using Mintmark.Infrastructure.Persistence;
using Mintmark.Infrastructure.Prices;
using Mintmark.Application.UseCases;

namespace Mintmark.Api.EndpointModules;

/// <summary>
/// Per-holding valuation: melt + rules-based collectible (ADR 0007) with the
/// itemized factor breakdown, confidence band and spot provenance. Every
/// computed valuation is persisted (Melt + Collectible rows) so history
/// stays explainable forever.
/// </summary>
public sealed class ValuationsModule : IEndpointModule
{
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/holdings/{id:long}/valuation", async (
            long id,
            MintmarkDbContext dbContext,
            PriceCache priceCache,
            ValuationService valuations,
            PriceOptions priceOptions,
            CancellationToken cancellationToken) =>
        {
            var holding = await dbContext.Holdings
                .Include(h => h.Revisions)
                .FirstOrDefaultAsync(h => h.Id == new HoldingId(id), cancellationToken);
            if (holding is null)
            {
                return ApiProblem.NotFound("Holding not found.");
            }

            if (holding.CoinTypeId is not { } coinTypeId)
            {
                return ApiProblem.Unprocessable(
                    "Valuation requires a cataloged coin type; generic holdings have no AMW to melt.");
            }

            var row = await dbContext.CoinTypes
                .Where(c => c.Id == coinTypeId)
                .Join(dbContext.Series, c => c.SeriesId, s => s.Id, (c, s) => new { CoinType = c, Series = s })
                .FirstOrDefaultAsync(cancellationToken);
            if (row is null)
            {
                return ApiProblem.Unprocessable("The holding's coin type no longer exists.");
            }

            var tier = await dbContext.SeriesDemandTiers
                .Where(t => t.SeriesId == row.Series.Id)
                .Select(t => (SeriesDemandTier?)t.Tier)
                .FirstOrDefaultAsync(cancellationToken)
                ?? SeriesDemandTier.Medium;

            var grading = await dbContext.Gradings
                .FirstOrDefaultAsync(g => g.HoldingId == holding.Id, cancellationToken);

            try
            {
                var valuation = await valuations.ValueAsync(
                    holding, row.CoinType, row.Series, tier, grading, cancellationToken);

                // Persist both rows for the historical record, with the spot
                // row provenance when one can be resolved.
                var spotIds = await priceCache.LatestRowIdsAsync(
                    new Currency(priceOptions.BaseCurrency),
                    cancellationToken);
                spotIds.TryGetValue(row.Series.Metal, out var spotRowId);

                dbContext.Valuations.Add(Domain.Entities.Valuation.Create(
                    holding.Id,
                    ValuationType.Melt,
                    valuation.Melt,
                    valuation.Provenance.Source,
                    valuation.Provenance.SourceTimestampUtc,
                    ValuationService.MeltMethod,
                    ValuationService.MeltMethodVersion,
                    1.0m,
                    1.0m,
                    spotRowId));
                dbContext.Valuations.Add(Domain.Entities.Valuation.Create(
                    holding.Id,
                    ValuationType.Collectible,
                    valuation.Collectible,
                    valuation.Provenance.Source,
                    valuation.Provenance.SourceTimestampUtc,
                    ValuationService.CollectibleMethod,
                    ValuationService.CollectibleMethodVersion,
                    valuation.ConfidenceBand.LowFraction,
                    valuation.ConfidenceBand.HighFraction,
                    spotRowId));
                await dbContext.SaveChangesAsync(cancellationToken);

                return Results.Ok(valuation);
            }
            catch (PriceUnavailableException)
            {
                return ApiProblem.ServiceUnavailable(
                    "No spot price data available yet; the poller has not recorded a successful fetch.");
            }
        })
        .WithTags("Valuations")
        .RequireAuthorization();
    }
}
