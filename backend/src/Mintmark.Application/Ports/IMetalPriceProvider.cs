using Mintmark.Domain;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Application.Ports;

/// <summary>An inclusive date range (used for price history queries).</summary>
/// <param name="Start">First day included.</param>
/// <param name="End">Last day included.</param>
public sealed record DateRange
{
    /// <summary>Initializes the range.</summary>
    /// <exception cref="ArgumentException">Thrown when the start is after the end.</exception>
    public DateRange(DateOnly start, DateOnly end)
    {
        if (start > end)
        {
            throw new ArgumentException($"Range start {start} is after end {end}.");
        }

        Start = start;
        End = end;
    }

    /// <summary>Gets the first day included.</summary>
    public DateOnly Start { get; }

    /// <summary>Gets the last day included.</summary>
    public DateOnly End { get; }

    /// <summary>Gets the number of days covered.</summary>
    public int DayCount => End.DayNumber - Start.DayNumber + 1;
}

/// <summary>A current spot quote from a market-data provider.</summary>
/// <param name="Price">Mid price per troy ounce.</param>
/// <param name="Bid">Bid per troy ounce.</param>
/// <param name="Ask">Ask per troy ounce.</param>
/// <param name="ProviderName">Provider identifier (or the literal <c>offline</c> label).</param>
/// <param name="SourceTimestampUtc">When the provider observed the quote.</param>
public sealed record ProviderQuote(
    Money Price,
    Money Bid,
    Money Ask,
    string ProviderName,
    DateTimeOffset SourceTimestampUtc);

/// <summary>One daily close from a provider's history.</summary>
/// <param name="Date">The close date.</param>
/// <param name="Close">Close price per troy ounce.</param>
public sealed record DailySpotQuote(DateOnly Date, Money Close);

/// <summary>
/// Port to market data: current spot quotes and daily history. Implemented
/// by Infrastructure against one or more metal price providers.
/// </summary>
public interface IMetalPriceProvider
{
    /// <summary>Gets the current spot quote for a metal and currency.</summary>
    /// <exception cref="NotSupportedException">May be thrown for unsupported currency/metal combinations.</exception>
    Task<ProviderQuote> GetCurrentAsync(
        MetalKind metal,
        Currency currency,
        CancellationToken cancellationToken = default);

    /// <summary>Gets daily closes for a metal and currency over an inclusive range.</summary>
    Task<IReadOnlyList<DailySpotQuote>> GetDailyHistoryAsync(
        MetalKind metal,
        Currency currency,
        DateRange range,
        CancellationToken cancellationToken = default);
}
