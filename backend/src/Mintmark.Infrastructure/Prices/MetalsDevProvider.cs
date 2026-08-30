using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mintmark.Application.Ports;
using Mintmark.Domain;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Infrastructure.Prices;

/// <summary>
/// metals.dev adapter (ADR 0004 primary). One <c>/v1/latest</c> call returns
/// gold, silver, platinum AND palladium together — the decisive factor for
/// primacy — so this provider memoizes the latest response per currency for
/// 55 seconds: a four-metal poll costs exactly one request.
/// </summary>
public sealed class MetalsDevProvider : IMetalPriceProvider
{
    /// <summary>The provider identifier recorded on SpotPrice rows.</summary>
    public const string ProviderName = "metalsdev";

    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly TimeProvider _clock;

    private readonly record struct Memo(LatestResponse Response, DateTimeOffset FetchedAt);
    private readonly Lock _memoLock = new();
    private Memo? _latestMemo;

    /// <summary>Initializes the adapter with the shared HTTP client.</summary>
    public MetalsDevProvider(HttpClient http, string? apiKey, TimeProvider? clock = null)
    {
        _http = http;
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _clock = clock ?? TimeProvider.System;

        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri("https://api.metals.dev/v1/");
        }
    }

    /// <summary>Gets a value indicating whether the provider is usable (needs an API key).</summary>
    public bool IsConfigured => _apiKey is not null;

    /// <inheritdoc />
    public async Task<ProviderQuote> GetCurrentAsync(MetalKind metal, Currency currency, CancellationToken cancellationToken = default)
    {
        if (_apiKey is null)
        {
            throw new InvalidOperationException("metals.dev requires an API key (MINTMARK_PRICE_PRIMARY_KEY).");
        }

        var response = await GetMemoizedLatestAsync(currency, cancellationToken);
        var price = metal switch
        {
            MetalKind.Gold => response.Metals?.Gold,
            MetalKind.Silver => response.Metals?.Silver,
            MetalKind.Platinum => response.Metals?.Platinum,
            MetalKind.Palladium => response.Metals?.Palladium,
            _ => null,
        } ?? throw new InvalidOperationException($"metals.dev response did not include {metal}.");

        var timestamp = response.Timestamp is > 0
            ? DateTimeOffset.FromUnixTimeSeconds(response.Timestamp.Value)
            : _clock.GetUtcNow();

        var mid = new Money(price, currency);
        return new ProviderQuote(
            mid,
            Bid: mid,
            Ask: mid,
            ProviderName,
            timestamp);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DailySpotQuote>> GetDailyHistoryAsync(
        MetalKind metal,
        Currency currency,
        DateRange range,
        CancellationToken cancellationToken = default)
    {
        if (_apiKey is null)
        {
            throw new InvalidOperationException("metals.dev requires an API key (MINTMARK_PRICE_PRIMARY_KEY).");
        }

        var url = $"timeseries?api_key={Uri.EscapeDataString(_apiKey)}"
            + $"&currency={currency.Code}&unit=toz"
            + $"&start_date={range.Start:yyyy-MM-dd}&end_date={range.End:yyyy-MM-dd}";

        var response = await _http.GetFromJsonAsync<TimeseriesResponse>(url, Json.Options, cancellationToken)
            ?? throw new InvalidOperationException("metals.dev timeseries returned an empty body.");

        var quotes = new List<DailySpotQuote>();
        foreach (var (dateText, rates) in response.Rates ?? [])
        {
            if (!DateOnly.TryParse(dateText, System.Globalization.CultureInfo.InvariantCulture, out var date) || date < range.Start || date > range.End)
            {
                continue;
            }

            var close = metal switch
            {
                MetalKind.Gold => rates?.Gold,
                MetalKind.Silver => rates?.Silver,
                MetalKind.Platinum => rates?.Platinum,
                MetalKind.Palladium => rates?.Palladium,
                _ => null,
            };

            if (close is > 0)
            {
                quotes.Add(new DailySpotQuote(date, new Money(close.Value, currency)));
            }
        }

        quotes.Sort((a, b) => a.Date.CompareTo(b.Date));
        return quotes;
    }

    private async Task<LatestResponse> GetMemoizedLatestAsync(Currency currency, CancellationToken cancellationToken)
    {
        var apiKey = _apiKey ?? throw new InvalidOperationException("metals.dev requires an API key.");
        Memo? usable;
        lock (_memoLock)
        {
            usable = _latestMemo is { } memo && currency.Code.Equals(memo.Response.Currency, StringComparison.OrdinalIgnoreCase)
                && _clock.GetUtcNow() - memo.FetchedAt < TimeSpan.FromSeconds(55)
                ? memo
                : null;
        }

        if (usable is { } hit)
        {
            return hit.Response;
        }

        var url = $"latest?api_key={Uri.EscapeDataString(apiKey)}&currency={currency.Code}&unit=toz";
        var response = await _http.GetFromJsonAsync<LatestResponse>(url, Json.Options, cancellationToken)
            ?? throw new InvalidOperationException("metals.dev latest returned an empty body.");

        if (string.IsNullOrEmpty(response.Currency))
        {
            response.Currency = currency.Code;
        }

        lock (_memoLock)
        {
            _latestMemo = new Memo(response, _clock.GetUtcNow());
        }

        return response;
    }

    /// <summary>Shared JSON contract DTOs (fixture-tested; see tests).</summary>
    internal static class Json
    {
        static Json() => Options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            
        };

        /// <summary>Gets the deserializer options matching metals.dev's shape.</summary>
        public static JsonSerializerOptions Options { get; }
    }

    /// <summary>metals.dev <c>/v1/latest</c> response shape (recorded fixture).</summary>
    internal sealed class LatestResponse
    {
        /// <summary>Gets or sets the status text.</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>Gets or sets the quote currency.</summary>
        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        /// <summary>Gets or sets the unit (toz).</summary>
        [JsonPropertyName("unit")]
        public string? Unit { get; set; }

        /// <summary>Gets or sets the metal prices per troy ounce.</summary>
        [JsonPropertyName("metals")]
        public MetalsBlock? Metals { get; set; }

        /// <summary>Gets or sets the provider's Unix timestamp.</summary>
        [JsonPropertyName("timestamp")]
        public long? Timestamp { get; set; }
    }

    /// <summary>The <c>metals</c> object of metals.dev responses.</summary>
    internal sealed class MetalsBlock
    {
        /// <summary>Gets or sets gold per ozt.</summary>
        [JsonPropertyName("gold")]
        public decimal? Gold { get; set; }

        /// <summary>Gets or sets silver per ozt.</summary>
        [JsonPropertyName("silver")]
        public decimal? Silver { get; set; }

        /// <summary>Gets or sets platinum per ozt.</summary>
        [JsonPropertyName("platinum")]
        public decimal? Platinum { get; set; }

        /// <summary>Gets or sets palladium per ozt.</summary>
        [JsonPropertyName("palladium")]
        public decimal? Palladium { get; set; }
    }

    /// <summary>metals.dev <c>/v1/timeseries</c> response shape (recorded fixture).</summary>
    internal sealed class TimeseriesResponse
    {
        /// <summary>Gets or sets the status text.</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>Gets or sets the daily rates keyed by ISO date.</summary>
        [JsonPropertyName("rates")]
        public Dictionary<string, MetalsBlock>? Rates { get; set; }
    }
}
