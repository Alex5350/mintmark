using Mintmark.Domain.ValueObjects;

namespace Mintmark.Domain.Entities;

/// <summary>
/// A spot price observation: metal, currency, price/bid/ask per troy ounce,
/// provider and the provider's source timestamp. Time-series; rolled up per
/// the retention policy in docs/architecture.md.
/// </summary>
public sealed class SpotPrice
{
    /// <summary>Parameterless constructor for EF Core materialization only.</summary>
    private SpotPrice()
    {
    }

    private SpotPrice(MetalKind metal, Money pricePerTroyOunce)
    {
        Metal = metal;
        PricePerTroyOunce = pricePerTroyOunce;
    }

    /// <summary>Gets the persistence-assigned identifier.</summary>
    public SpotPriceId Id { get; private set; }

    /// <summary>Gets the priced metal.</summary>
    public MetalKind Metal { get; private set; }

    /// <summary>Gets the quote currency.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Gets the mid price per troy ounce.</summary>
    public Money PricePerTroyOunce { get; private set; }

    /// <summary>Gets the bid per troy ounce.</summary>
    public Money BidPerTroyOunce { get; private set; }

    /// <summary>Gets the ask per troy ounce.</summary>
    public Money AskPerTroyOunce { get; private set; }

    /// <summary>Gets the provider name.</summary>
    public string ProviderName { get; private set; } = string.Empty;

    /// <summary>Gets the provider's source timestamp (UTC).</summary>
    public DateTimeOffset SourceTimestampUtc { get; private set; }

    /// <summary>Gets when the row was ingested (UTC).</summary>
    public DateTimeOffset IngestedAtUtc { get; private set; }

    /// <summary>Creates a spot row, enforcing currency consistency.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the provider is missing, the price is not positive, or the
    /// price/bid/ask currencies differ.
    /// </exception>
    public static SpotPrice Create(
        MetalKind metal,
        Money pricePerTroyOunce,
        Money bidPerTroyOunce,
        Money askPerTroyOunce,
        string providerName,
        DateTimeOffset sourceTimestampUtc,
        DateTimeOffset? ingestedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name is required.", nameof(providerName));
        }

        if (pricePerTroyOunce.Amount <= 0m)
        {
            throw new ArgumentException(
                $"Spot price must be positive; got {pricePerTroyOunce}.", nameof(pricePerTroyOunce));
        }

        if (pricePerTroyOunce.Currency != bidPerTroyOunce.Currency
            || pricePerTroyOunce.Currency != askPerTroyOunce.Currency)
        {
            throw new ArgumentException(
                "Price, bid and ask must share one currency; cross-currency quotes are not supported.");
        }

        return new SpotPrice(metal, pricePerTroyOunce)
        {
            Currency = pricePerTroyOunce.Currency,
            BidPerTroyOunce = bidPerTroyOunce,
            AskPerTroyOunce = askPerTroyOunce,
            ProviderName = providerName.Trim(),
            SourceTimestampUtc = sourceTimestampUtc.ToUniversalTime(),
            IngestedAtUtc = ingestedAtUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow,
        };
    }
}

/// <summary>
/// A historical daily close for a metal. Natural key: (metal, currency,
/// date). Backfilled on first run so charts are never empty on day one.
/// </summary>
public sealed class SpotPriceDaily
{
    /// <summary>Parameterless constructor for EF Core materialization only.</summary>
    private SpotPriceDaily()
    {
    }

    private SpotPriceDaily(MetalKind metal, DateOnly date, Money close)
    {
        Metal = metal;
        Date = date;
        Close = close;
    }

    /// <summary>Gets the priced metal (part of the natural key).</summary>
    public MetalKind Metal { get; private set; }

    /// <summary>Gets the close date (part of the natural key).</summary>
    public DateOnly Date { get; private set; }

    /// <summary>Gets the quote currency (part of the natural key).</summary>
    public Currency Currency { get; private set; }

    /// <summary>Gets the close price per troy ounce.</summary>
    public Money Close { get; private set; }

    /// <summary>Gets the provider the close came from, if known.</summary>
    public string? ProviderName { get; private set; }

    /// <summary>Gets when the row was ingested (UTC).</summary>
    public DateTimeOffset IngestedAtUtc { get; private set; }

    /// <summary>Creates a daily close row, enforcing its invariants.</summary>
    /// <exception cref="ArgumentException">Thrown when the close is not positive.</exception>
    public static SpotPriceDaily Create(
        MetalKind metal,
        Currency currency,
        DateOnly date,
        Money close,
        string? providerName = null,
        DateTimeOffset? ingestedAtUtc = null)
    {
        if (close.Amount <= 0m)
        {
            throw new ArgumentException($"Daily close must be positive; got {close}.", nameof(close));
        }

        if (close.Currency != currency)
        {
            throw new ArgumentException(
                $"Close currency {close.Currency} does not match {currency}.", nameof(close));
        }

        return new SpotPriceDaily(metal, date, close)
        {
            Currency = currency,
            ProviderName = providerName,
            IngestedAtUtc = ingestedAtUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow,
        };
    }
}
