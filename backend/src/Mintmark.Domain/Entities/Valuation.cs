using Mintmark.Domain.ValueObjects;

namespace Mintmark.Domain.Entities;

/// <summary>
/// One explainable valuation of a holding. Carries the method + method
/// version that produced it, a confidence band (fractions of the central
/// value: Low &lt;= 1 &lt;= High), and enough provenance — spot source and
/// timestamp — to stay explainable forever even after spot rows roll off per
/// the retention policy.
/// </summary>
public sealed class Valuation
{
    /// <summary>Parameterless constructor for EF Core materialization only.</summary>
    private Valuation()
    {
    }

    private Valuation(Money value)
    {
        Value = value;
    }

    /// <summary>Gets the persistence-assigned identifier.</summary>
    public ValuationId Id { get; private set; }

    /// <summary>Gets the valued holding.</summary>
    public HoldingId HoldingId { get; private set; }

    /// <summary>Gets the valuation kind (melt or collectible).</summary>
    public ValuationType Type { get; private set; }

    /// <summary>Gets the computed value.</summary>
    public Money Value { get; private set; }

    /// <summary>Gets the spot row this valuation derived from, when one was persisted.</summary>
    public SpotPriceId? DerivedFromSpotPriceId { get; private set; }

    /// <summary>Gets the spot provider name recorded at computation time (provenance snapshot).</summary>
    public string SpotProviderName { get; private set; } = string.Empty;

    /// <summary>Gets the spot source timestamp recorded at computation time (UTC).</summary>
    public DateTimeOffset SpotSourceTimestampUtc { get; private set; }

    /// <summary>Gets the valuation method (e.g. <c>melt</c>, <c>rules-premium</c>).</summary>
    public string Method { get; private set; } = string.Empty;

    /// <summary>Gets the version of the method (e.g. <c>melt-v1</c>, <c>rules-v1</c>).</summary>
    public string MethodVersion { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the lower confidence bound as a fraction of the value (in [0, 1];
    /// value x Low is the pessimistic bound).
    /// </summary>
    public decimal ConfidenceBandLow { get; private set; }

    /// <summary>
    /// Gets the upper confidence bound as a fraction of the value (>= 1;
    /// value x High is the optimistic bound).
    /// </summary>
    public decimal ConfidenceBandHigh { get; private set; }

    /// <summary>Gets when the valuation was computed (UTC).</summary>
    public DateTimeOffset ComputedAtUtc { get; private set; }

    /// <summary>Creates a valuation, enforcing its invariants.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown when method metadata is missing or the confidence band is malformed.
    /// </exception>
    public static Valuation Create(
        HoldingId holdingId,
        ValuationType type,
        Money value,
        string spotProviderName,
        DateTimeOffset spotSourceTimestampUtc,
        string method,
        string methodVersion,
        decimal confidenceBandLow,
        decimal confidenceBandHigh,
        SpotPriceId? derivedFromSpotPriceId = null,
        DateTimeOffset? computedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(methodVersion))
        {
            throw new ArgumentException("Method and method version are required.");
        }

        if (string.IsNullOrWhiteSpace(spotProviderName))
        {
            throw new ArgumentException("Spot provider name is required (provenance).", nameof(spotProviderName));
        }

        if (confidenceBandLow < 0m || confidenceBandLow > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidenceBandLow), confidenceBandLow, "Confidence band low must be a fraction in [0, 1].");
        }

        if (confidenceBandHigh < 1m || confidenceBandHigh < confidenceBandLow)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidenceBandHigh), confidenceBandHigh, "Confidence band high must be >= 1 and >= the low bound.");
        }

        return new Valuation(value)
        {
            HoldingId = holdingId,
            Type = type,
            DerivedFromSpotPriceId = derivedFromSpotPriceId,
            SpotProviderName = spotProviderName.Trim(),
            SpotSourceTimestampUtc = spotSourceTimestampUtc.ToUniversalTime(),
            Method = method.Trim(),
            MethodVersion = methodVersion.Trim(),
            ConfidenceBandLow = confidenceBandLow,
            ConfidenceBandHigh = confidenceBandHigh,
            ComputedAtUtc = computedAtUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow,
        };
    }
}
