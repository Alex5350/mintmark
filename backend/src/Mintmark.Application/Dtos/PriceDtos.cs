using Mintmark.Domain;
using Mintmark.Domain.ValueObjects;
using Mintmark.Application.Ports;

namespace Mintmark.Application.Dtos;

/// <summary>A spot quote with staleness and provenance.</summary>
/// <param name="Metal">The priced metal.</param>
/// <param name="Currency">The quote currency.</param>
/// <param name="Price">Mid price per troy ounce.</param>
/// <param name="Bid">Bid per troy ounce.</param>
/// <param name="Ask">Ask per troy ounce.</param>
/// <param name="Provider">Provider name (or the literal <c>offline</c> label).</param>
/// <param name="SourceTimestampUtc">When the provider observed the quote.</param>
/// <param name="IsStale">True when the quote is older than the freshness window.</param>
public sealed record SpotQuote(
    MetalKind Metal,
    Currency Currency,
    Money Price,
    Money Bid,
    Money Ask,
    string Provider,
    DateTimeOffset SourceTimestampUtc,
    bool IsStale);

/// <summary>How a chart series was reduced for transport.</summary>
public enum ChartDownsampleMethod
{
    /// <summary>No downsampling applied.</summary>
    None,

    /// <summary>Largest-triangle-three-buckets reduction.</summary>
    Lttb,

    /// <summary>Aggregated per day.</summary>
    DailyAggregate,

    /// <summary>Aggregated per week.</summary>
    WeeklyAggregate,

    /// <summary>Aggregated per month.</summary>
    MonthlyAggregate,
}

/// <summary>One point of a price chart.</summary>
/// <param name="Date">The close date.</param>
/// <param name="Close">The close price per troy ounce.</param>
public sealed record ChartPoint(DateOnly Date, Money Close);

/// <summary>A downsampled price chart series.</summary>
/// <param name="Metal">The charted metal.</param>
/// <param name="Currency">The chart currency.</param>
/// <param name="Range">The covered date range.</param>
/// <param name="Points">The (possibly downsampled) close points, ascending by date.</param>
/// <param name="DownsampleMethod">How the points were reduced.</param>
public sealed record ChartSeries(
    MetalKind Metal,
    Currency Currency,
    DateRange Range,
    IReadOnlyList<ChartPoint> Points,
    ChartDownsampleMethod DownsampleMethod);

/// <summary>One point of the gold/silver ratio series.</summary>
/// <param name="Date">The date.</param>
/// <param name="Ratio">Gold price per troy ounce divided by silver price per troy ounce.</param>
public sealed record GoldSilverRatioPoint(DateOnly Date, decimal Ratio);
