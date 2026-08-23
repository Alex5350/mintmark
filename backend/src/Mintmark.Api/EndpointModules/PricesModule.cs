using Mintmark.Api;
using Mintmark.Domain;
using Mintmark.Domain.ValueObjects;
using Mintmark.Infrastructure;
using Mintmark.Infrastructure.Prices;

namespace Mintmark.Api.EndpointModules;

/// <summary>
/// Public spot-price endpoints: current quotes (cached, staleness flagged),
/// chart series (server-side LTTB/bucketed reduction with the method stamped
/// on the response) and the gold/silver ratio series.
/// </summary>
public sealed class PricesModule : IEndpointModule
{
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/prices").WithTags("Prices");

        group.MapGet("/current", async (
            string? metals,
            PriceCache priceCache,
            PriceOptions priceOptions,
            CancellationToken cancellationToken) =>
        {
            var requested = ParseMetals(metals);
            if (requested.Count == 0)
            {
                return ApiProblem.Validation(new Dictionary<string, string[]>
                {
                    ["metals"] = ["Provide at least one of: gold, silver, platinum, palladium."],
                });
            }

            var currency = new Currency(priceOptions.BaseCurrency);
            var quotes = new List<CurrentPriceResponse>();
            foreach (var metal in requested)
            {
                try
                {
                    var result = await priceCache.GetAsync(metal, currency, cancellationToken);
                    quotes.Add(new CurrentPriceResponse(
                        metal,
                        currency.Code,
                        result.Quote.Price.Amount,
                        result.Quote.Bid.Amount,
                        result.Quote.Ask.Amount,
                        result.Quote.ProviderName,
                        result.Quote.SourceTimestampUtc,
                        result.Stale,
                        result.StaleSince));
                }
                catch (PriceUnavailableException)
                {
                    // Missing metals are omitted; a full miss returns 503.
                }
            }

            if (quotes.Count == 0)
            {
                return ApiProblem.ServiceUnavailable(
                    "No spot price data available yet; the poller has not recorded a successful fetch.");
            }

            return Results.Ok(quotes);
        });

        group.MapGet("/chart", async (
            string metal,
            string? range,
            PriceChartService charts,
            CancellationToken cancellationToken) =>
        {
            var parsed = ParseMetal(metal);
            if (parsed is null)
            {
                return ApiProblem.Validation(new Dictionary<string, string[]>
                {
                    ["metal"] = ["Metal must be one of: gold, silver, platinum, palladium."],
                });
            }

            ChartRange parsedRange;
            try
            {
                parsedRange = PriceChartService.ParseRange(range);
            }
            catch (ArgumentException ex)
            {
                return ApiProblem.Validation(new Dictionary<string, string[]> { ["range"] = [ex.Message] });
            }

            var series = await charts.GetChartAsync(parsed.Value, parsedRange, cancellationToken);
            return Results.Ok(series);
        });

        group.MapGet("/ratio", async (
            string? range,
            PriceChartService charts,
            CancellationToken cancellationToken) =>
        {
            ChartRange parsedRange;
            try
            {
                parsedRange = PriceChartService.ParseRange(range);
            }
            catch (ArgumentException ex)
            {
                return ApiProblem.Validation(new Dictionary<string, string[]> { ["range"] = [ex.Message] });
            }

            var points = await charts.GetRatioAsync(parsedRange, cancellationToken);
            return Results.Ok(points);
        });
    }

    private static List<MetalKind> ParseMetals(string? metals)
    {
        if (string.IsNullOrWhiteSpace(metals))
        {
            return [MetalKind.Gold, MetalKind.Silver];
        }

        var parsed = metals.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseMetal)
            .OfType<MetalKind>()
            .Distinct()
            .ToList();
        return parsed;
    }

    private static MetalKind? ParseMetal(string? metal) => metal?.Trim().ToLowerInvariant() switch
    {
        "gold" => MetalKind.Gold,
        "silver" => MetalKind.Silver,
        "platinum" => MetalKind.Platinum,
        "palladium" => MetalKind.Palladium,
        _ => null,
    };
}

/// <summary>Current spot quote with staleness and provenance.</summary>
/// <param name="Metal">The priced metal.</param>
/// <param name="Currency">Quote currency code.</param>
/// <param name="Price">Mid price per troy ounce.</param>
/// <param name="Bid">Bid per troy ounce.</param>
/// <param name="Ask">Ask per troy ounce.</param>
/// <param name="Provider">Provider that served the row.</param>
/// <param name="SourceTimestampUtc">When the provider observed it.</param>
/// <param name="IsStale">True when older than the freshness window.</param>
/// <param name="StaleSince">Set when stale: the quote's source timestamp.</param>
public sealed record CurrentPriceResponse(
    MetalKind Metal,
    string Currency,
    decimal Price,
    decimal Bid,
    decimal Ask,
    string Provider,
    DateTimeOffset SourceTimestampUtc,
    bool IsStale,
    DateTimeOffset? StaleSince);
