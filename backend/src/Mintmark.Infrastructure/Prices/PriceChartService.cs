using Microsoft.EntityFrameworkCore;
using Mintmark.Application.Dtos;
using Mintmark.Application.Ports;
using Mintmark.Infrastructure.Persistence;
using Mintmark.Domain;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Infrastructure.Prices;

/// <summary>Supported chart ranges (wire form: 1D, 1W, 1M, 3M, 6M, 1Y, 5Y, MAX).</summary>
public enum ChartRange
{
    /// <summary>Last 24 hours (from raw ticks).</summary>
    OneDay,

    /// <summary>Last 7 days (ticks bucketed per day).</summary>
    OneWeek,

    /// <summary>Last 30 days (daily closes).</summary>
    OneMonth,

    /// <summary>Last 90 days.</summary>
    ThreeMonths,

    /// <summary>Last 180 days.</summary>
    SixMonths,

    /// <summary>Last 365 days.</summary>
    OneYear,

    /// <summary>Last 5 years.</summary>
    FiveYears,

    /// <summary>Everything stored.</summary>
    Max,
}

/// <summary>
/// Server-side chart reduction (architecture.md): LTTB for 3M+ series,
/// bucketed averages below, with the method stamped on the response. Reads
/// authoritative Postgres rows only — never providers.
/// </summary>
public sealed class PriceChartService(MintmarkDbContext dbContext, PriceOptions options)
{
    private const int LttbThreshold = 300;

    /// <summary>Parses the wire range token (case-insensitive).</summary>
    public static ChartRange ParseRange(string? token) => token?.ToUpperInvariant() switch
    {
        null or "" or "1M" => ChartRange.OneMonth,
        "1D" => ChartRange.OneDay,
        "1W" => ChartRange.OneWeek,
        "3M" => ChartRange.ThreeMonths,
        "6M" => ChartRange.SixMonths,
        "1Y" => ChartRange.OneYear,
        "5Y" => ChartRange.FiveYears,
        "MAX" => ChartRange.Max,
        _ => throw new ArgumentException(
            $"Unknown range '{token}'. Use one of: 1D, 1W, 1M, 3M, 6M, 1Y, 5Y, MAX."),
    };

    /// <summary>Builds the chart series for a metal over a range, downsampling server-side.</summary>
    public async Task<ChartSeries> GetChartAsync(
        MetalKind metal,
        ChartRange range,
        CancellationToken cancellationToken = default)
    {
        var currency = new Currency(options.BaseCurrency);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (start, end) = Bounds(range, today);

        if (range is ChartRange.OneDay or ChartRange.OneWeek)
        {
            var since = range == ChartRange.OneDay
                ? DateTimeOffset.UtcNow.AddDays(-1)
                : DateTimeOffset.UtcNow.AddDays(-7);

            var ticks = await dbContext.SpotPrices
                .Where(p => p.Metal == metal && p.Currency == currency && p.SourceTimestampUtc >= since)
                .OrderBy(p => p.SourceTimestampUtc)
                .ToListAsync(cancellationToken);

            if (ticks.Count == 0)
            {
                return Empty(metal, currency, start, end);
            }

            // Bucket raw ticks per calendar day (avg of mids); 1D covers one
            // bucket, 1W covers seven — honest daily aggregation of ticks.
            var buckets = ticks
                .GroupBy(t => DateOnly.FromDateTime(t.SourceTimestampUtc.UtcDateTime))
                .Select(g => new ChartPoint(g.Key, new Money(Math.Round(g.Average(t => t.PricePerTroyOunce.Amount), 4), currency)))
                .OrderBy(p => p.Date)
                .ToList();

            return new ChartSeries(metal, currency, new DateRange(buckets[0].Date, buckets[^1].Date), buckets, ChartDownsampleMethod.DailyAggregate);
        }

        var closes = await dbContext.SpotPriceDaily
            .Where(d => d.Metal == metal && d.Currency == currency && d.Date >= start && d.Date <= end)
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken);

        if (closes.Count == 0)
        {
            return Empty(metal, currency, start, end);
        }

        var points = closes.Select(d => new ChartPoint(d.Date, d.Close)).ToList();
        var method = range switch
        {
            ChartRange.OneMonth => ChartDownsampleMethod.None, // already daily, <= 31 points
            _ => ChartDownsampleMethod.Lttb,
        };

        if (method == ChartDownsampleMethod.Lttb && points.Count > LttbThreshold)
        {
            points = [.. Lttb(points, LttbThreshold)];
        }

        return new ChartSeries(metal, currency, new DateRange(points[0].Date, points[^1].Date), points, method);
    }

    /// <summary>Builds the gold/silver ratio series over daily closes (joined on date).</summary>
    public async Task<IReadOnlyList<GoldSilverRatioPoint>> GetRatioAsync(
        ChartRange range,
        CancellationToken cancellationToken = default)
    {
        var currency = new Currency(options.BaseCurrency);
        var (start, end) = Bounds(range, DateOnly.FromDateTime(DateTime.UtcNow));

        var gold = await DailyMapAsync(MetalKind.Gold, currency, start, end, cancellationToken);
        var silver = await DailyMapAsync(MetalKind.Silver, currency, start, end, cancellationToken);

        var joined = gold.Keys
            .Where(silver.ContainsKey)
            .OrderBy(d => d)
            .Select(d => new GoldSilverRatioPoint(d, gold[d] / silver[d]))
            .ToList();

        if (joined.Count > LttbThreshold)
        {
            joined = [.. LttbRatio(joined, LttbThreshold)];
        }

        return joined;
    }

    private async Task<Dictionary<DateOnly, decimal>> DailyMapAsync(
        MetalKind metal,
        Currency currency,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.SpotPriceDaily
            .Where(d => d.Metal == metal && d.Currency == currency && d.Date >= start && d.Date <= end)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(d => d.Date, d => d.Close.Amount);
    }

    private static (DateOnly Start, DateOnly End) Bounds(ChartRange range, DateOnly today) =>
        range switch
        {
            ChartRange.OneDay => (today.AddDays(-1), today),
            ChartRange.OneWeek => (today.AddDays(-7), today),
            ChartRange.OneMonth => (today.AddMonths(-1), today),
            ChartRange.ThreeMonths => (today.AddMonths(-3), today),
            ChartRange.SixMonths => (today.AddMonths(-6), today),
            ChartRange.OneYear => (today.AddYears(-1), today),
            ChartRange.FiveYears => (today.AddYears(-5), today),
            _ => (new DateOnly(1, 1, 1), today),
        };

    private static ChartSeries Empty(MetalKind metal, Currency currency, DateOnly start, DateOnly end) =>
        new(metal, currency, new DateRange(start, end), [], ChartDownsampleMethod.None);

    /// <summary>
    /// Largest-Triangle-Three-Buckets downsampling. X is the point index
    /// (dates roughly uniform); each bucket keeps the point maximizing the
    /// triangle area with the previously kept point and the next bucket's
    /// average.
    /// </summary>
    internal static List<ChartPoint> Lttb(IReadOnlyList<ChartPoint> points, int threshold)
    {
        if (points.Count <= threshold || threshold < 3)
        {
            return [.. points];
        }

        var sampled = new List<ChartPoint>(threshold) { points[0] };
        var every = (double)(points.Count - 2) / (threshold - 2);
        var previousIndex = 0;

        for (var i = 0; i < threshold - 2; i++)
        {
            var nextStart = (int)Math.Floor((i + 1) * every) + 1;
            var nextEnd = Math.Min((int)Math.Floor((i + 2) * every) + 1, points.Count - 1);

            double averageX = 0, averageY = 0;
            var bucketSize = nextEnd - nextStart + 1;
            for (var j = nextStart; j <= nextEnd; j++)
            {
                averageX += j;
                averageY += (double)points[j].Close.Amount;
            }

            averageX /= bucketSize;
            averageY /= bucketSize;

            var rangeStart = (int)Math.Floor(i * every) + 1;
            var rangeEnd = (int)Math.Floor((i + 1) * every) + 1;

            double ax = previousIndex;
            double ay = (double)points[previousIndex].Close.Amount;
            var maxArea = -1.0;
            var bestIndex = rangeStart;

            for (var j = rangeStart; j <= rangeEnd; j++)
            {
                var area = Math.Abs(
                    ((ax - averageX) * ((double)points[j].Close.Amount - ay))
                    - ((ax - j) * (averageY - ay)));
                if (area > maxArea)
                {
                    maxArea = area;
                    bestIndex = j;
                }
            }

            sampled.Add(points[bestIndex]);
            previousIndex = bestIndex;
        }

        sampled.Add(points[^1]);
        return sampled;
    }

    private static List<GoldSilverRatioPoint> LttbRatio(List<GoldSilverRatioPoint> points, int threshold)
    {
        if (points.Count <= threshold || threshold < 3)
        {
            return [.. points];
        }

        var converted = points
            .Select(p => new ChartPoint(p.Date, new Money(p.Ratio, "USD")))
            .ToList();
        return [.. Lttb(converted, threshold).Select(p => new GoldSilverRatioPoint(p.Date, p.Close.Amount))];
    }
}
