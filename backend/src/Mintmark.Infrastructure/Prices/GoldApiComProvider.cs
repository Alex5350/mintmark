using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Mintmark.Application.Ports;
using Mintmark.Domain;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Infrastructure.Prices;

/// <summary>
/// gold-api.com adapter (ADR 0004 fallback). Spot is keyless and unlimited;
/// the <c>/history</c> endpoint (aggregated daily average) is rate limited to
/// 10 requests/hour with a free key, so range pulls refuse windows longer
/// than ten days — deep backfill is metals.dev timeseries territory.
/// </summary>
public sealed class GoldApiComProvider : IMetalPriceProvider
{
    /// <summary>The provider identifier recorded on SpotPrice rows.</summary>
    public const string ProviderName = "goldapicom";

    /// <summary>The maximum history window this adapter will pull (per-day calls, 10/hour free tier).</summary>
    public const int MaxHistoryDays = 10;

    private static readonly Dictionary<MetalKind, string> Symbols = new Dictionary<MetalKind, string>
    {
        [MetalKind.Gold] = "XAU",
        [MetalKind.Silver] = "XAG",
        [MetalKind.Platinum] = "XPT",
        [MetalKind.Palladium] = "PD",
    };

    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly TimeProvider _clock;

    /// <summary>Initializes the adapter with the shared HTTP client.</summary>
    public GoldApiComProvider(HttpClient http, string? apiKey = null, TimeProvider? clock = null)
    {
        _http = http;
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _clock = clock ?? TimeProvider.System;

        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri("https://api.gold-api.com/");
        }
    }

    /// <summary>Gets a value indicating whether the provider is usable (spot is keyless).</summary>
    public static bool IsConfigured => true;

    /// <inheritdoc />
    public async Task<ProviderQuote> GetCurrentAsync(MetalKind metal, Currency currency, CancellationToken cancellationToken = default)
    {
        if (!Symbols.TryGetValue(metal, out var symbol))
        {
            throw new NotSupportedException($"gold-api.com does not quote {metal}.");
        }

        if (!currency.Code.Equals("USD", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("gold-api.com quotes USD only.");
        }

        var price = await _http.GetFromJsonAsync<PriceResponse>($"price/{symbol}", cancellationToken)
            ?? throw new InvalidOperationException("gold-api.com price returned an empty body.");

        var timestamp = DateTimeOffset.TryParse(price.UpdatedAt, out var parsed) ? parsed : _clock.GetUtcNow();
        var mid = new Money(price.Price, "USD");
        return new ProviderQuote(mid, Bid: mid, Ask: mid, ProviderName, timestamp);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DailySpotQuote>> GetDailyHistoryAsync(
        MetalKind metal,
        Currency currency,
        DateRange range,
        CancellationToken cancellationToken = default)
    {
        if (!Symbols.TryGetValue(metal, out var symbol))
        {
            throw new NotSupportedException($"gold-api.com does not quote {metal}.");
        }

        if (range.DayCount > MaxHistoryDays)
        {
            throw new NotSupportedException(
                $"gold-api.com history pulls at most {MaxHistoryDays} days per call (free tier: 10 req/hour); metals.dev serves deep backfills.");
        }

        var quotes = new List<DailySpotQuote>();
        for (var date = range.Start; date <= range.End; date = date.AddDays(1))
        {
            var url = $"history/{symbol}?date={date:yyyy-MM-dd}";
            if (_apiKey is not null)
            {
                url += $"&api_key={Uri.EscapeDataString(_apiKey)}";
            }

            var history = await _http.GetFromJsonAsync<HistoryResponse>(url, cancellationToken);
            if (history?.Price is > 0)
            {
                quotes.Add(new DailySpotQuote(date, new Money(history.Price.Value, currency)));
            }
        }

        return quotes;
    }

    /// <summary>gold-api.com <c>/price/{symbol}</c> response shape (recorded fixture).</summary>
    internal sealed class PriceResponse
    {
        /// <summary>Gets or sets the metal display name.</summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>Gets or sets the price per troy ounce (USD).</summary>
        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        /// <summary>Gets or sets the symbol (XAU, XAG, ...).</summary>
        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        /// <summary>Gets or sets the provider's updatedAt timestamp.</summary>
        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }
    }

    /// <summary>gold-api.com <c>/history/{symbol}</c> response shape (recorded fixture).</summary>
    internal sealed class HistoryResponse
    {
        /// <summary>Gets or sets the history date.</summary>
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        /// <summary>Gets or sets the aggregated daily average price.</summary>
        [JsonPropertyName("price")]
        public decimal? Price { get; set; }
    }
}
