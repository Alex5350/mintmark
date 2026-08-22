using Mintmark.Application.Dtos;
using Mintmark.Application.Ports;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Domain.Services;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Application.UseCases;

/// <summary>
/// Valuation orchestration: melt via <see cref="MeltValuation"/> and the
/// collectible estimate via <see cref="PremiumCalculator"/>, stamped with
/// method versions so every historical valuation stays explainable.
/// </summary>
public sealed class ValuationService
{
    /// <summary>The melt method identifier.</summary>
    public const string MeltMethod = "melt";

    /// <summary>The melt method version.</summary>
    public const string MeltMethodVersion = "melt-v1";

    /// <summary>The collectible method identifier (ADR 0007 rules).</summary>
    public const string CollectibleMethod = "rules-premium";

    /// <summary>The collectible method version stamped on valuations.</summary>
    public const string CollectibleMethodVersion = "rules-v1";

    private readonly IMetalPriceProvider _priceProvider;
    private readonly PremiumCalculator _premiumCalculator;

    /// <summary>Initializes the service with its spot-price port and premium weights.</summary>
    public ValuationService(IMetalPriceProvider priceProvider, PremiumCalculator? premiumCalculator = null)
    {
        _priceProvider = priceProvider;
        _premiumCalculator = premiumCalculator ?? new PremiumCalculator();
    }

    /// <summary>
    /// Values one holding: fetches spot for the series metal, computes melt,
    /// applies the rules-based premium, and returns the explainable DTO
    /// (multiplier, itemized factors, confidence band, provenance). The
    /// collectible estimate is stamped <c>rules-v1</c>.
    /// </summary>
    /// <param name="holding">The holding to value.</param>
    /// <param name="coinType">Its catalog row.</param>
    /// <param name="series">The row's series (carries the metal).</param>
    /// <param name="seriesDemandTier">The series demand tier (reference data).</param>
    /// <param name="grading">The holding's grading, when graded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HoldingValuation> ValueAsync(
        Holding holding,
        CoinType coinType,
        Series series,
        SeriesDemandTier seriesDemandTier,
        Grading? grading = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(holding);
        ArgumentNullException.ThrowIfNull(coinType);
        ArgumentNullException.ThrowIfNull(series);

        var currency = holding.EffectivePurchasePricePerUnit.Currency;
        var quote = await _priceProvider.GetCurrentAsync(series.Metal, currency, cancellationToken);

        var melt = MeltValuation.Estimate(coinType.ActualMetalWeight, holding.EffectiveQuantity, quote.Price);
        var premium = _premiumCalculator.Estimate(coinType, grading, seriesDemandTier);
        var estimate = premium.ApplyTo(melt);

        // Persistence (Valuation rows for Melt and Collectible, with the
        // method versions above) is done by the calling layer from this DTO.
        return new HoldingValuation(
            holding.Id,
            melt,
            estimate.Collectible,
            estimate.Premium,
            premium.Multiplier,
            premium.Factors.Select(f => new PremiumFactorDto(f.FactorName, f.Multiplier, f.Rationale)).ToList(),
            new ConfidenceBandDto(
                premium.BandLowFraction,
                premium.BandHighFraction,
                estimate.Collectible * premium.BandLowFraction,
                estimate.Collectible * premium.BandHighFraction),
            new ValuationProvenance(
                quote.Price,
                quote.ProviderName,
                quote.SourceTimestampUtc,
                CollectibleMethod,
                CollectibleMethodVersion),
            DateTimeOffset.UtcNow);
    }
}
