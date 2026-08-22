using Mintmark.Domain;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Application.Dtos;

/// <summary>One explainable premium factor line (see ADR 0007).</summary>
/// <param name="FactorName">Stable machine name of the factor.</param>
/// <param name="Multiplier">The multiplier this factor contributed.</param>
/// <param name="Rationale">Why the factor applied, citing the input data.</param>
public sealed record PremiumFactorDto(string FactorName, decimal Multiplier, string Rationale);

/// <summary>Confidence band around a valuation: fractions of the central value and absolute bounds.</summary>
/// <param name="LowFraction">Lower bound as a fraction (e.g. 0.75).</param>
/// <param name="HighFraction">Upper bound as a fraction (e.g. 1.25).</param>
/// <param name="LowValue">Pessimistic absolute bound.</param>
/// <param name="HighValue">Optimistic absolute bound.</param>
public sealed record ConfidenceBandDto(
    decimal LowFraction,
    decimal HighFraction,
    Money LowValue,
    Money HighValue);

/// <summary>Where a valuation's spot came from and what produced it.</summary>
/// <param name="SpotPricePerTroyOunce">The spot price used.</param>
/// <param name="Source">Provider name (or <c>offline</c> label).</param>
/// <param name="SourceTimestampUtc">When the provider observed the quote.</param>
/// <param name="Method">Valuation method (e.g. <c>rules-premium</c>).</param>
/// <param name="MethodVersion">Method version (e.g. <c>rules-v1</c>).</param>
public sealed record ValuationProvenance(
    Money SpotPricePerTroyOunce,
    string Source,
    DateTimeOffset SourceTimestampUtc,
    string Method,
    string MethodVersion);

/// <summary>
/// A full holding valuation: melt, collectible estimate, itemized premium
/// factors, confidence band, and provenance. Every number is explainable
/// line by line.
/// </summary>
public sealed record HoldingValuation
{
    /// <summary>Initializes the valuation.</summary>
    public HoldingValuation(
        HoldingId holdingId,
        Money melt,
        Money collectible,
        Money premium,
        decimal premiumMultiplier,
        IReadOnlyList<PremiumFactorDto> premiumFactors,
        ConfidenceBandDto confidenceBand,
        ValuationProvenance provenance,
        DateTimeOffset computedAtUtc)
    {
        HoldingId = holdingId;
        Melt = melt;
        Collectible = collectible;
        Premium = premium;
        PremiumMultiplier = premiumMultiplier;
        PremiumFactors = premiumFactors;
        ConfidenceBand = confidenceBand;
        Provenance = provenance;
        ComputedAtUtc = computedAtUtc;
    }

    /// <summary>Gets the valued holding.</summary>
    public HoldingId HoldingId { get; }

    /// <summary>Gets the melt value (ASW/AGW x quantity x spot).</summary>
    public Money Melt { get; }

    /// <summary>Gets the collectible estimate: melt x Π(factors).</summary>
    public Money Collectible { get; }

    /// <summary>Gets the numismatic premium: collectible − melt.</summary>
    public Money Premium { get; }

    /// <summary>Gets the premium multiplier Π(factors).</summary>
    public decimal PremiumMultiplier { get; }

    /// <summary>Gets the itemized factor breakdown behind the multiplier.</summary>
    public IReadOnlyList<PremiumFactorDto> PremiumFactors { get; }

    /// <summary>Gets the confidence band around the collectible estimate.</summary>
    public ConfidenceBandDto ConfidenceBand { get; }

    /// <summary>Gets the provenance of the spot quote and method.</summary>
    public ValuationProvenance Provenance { get; }

    /// <summary>Gets when the valuation was computed (UTC).</summary>
    public DateTimeOffset ComputedAtUtc { get; }
}
