using Microsoft.Extensions.Logging;
using Mintmark.Application.Ports;
using Mintmark.Domain;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Infrastructure.Prices;

/// <summary>
/// Configured-order failover chain over the ADR 0004 providers: primary
/// first, fallback next. Each returned quote carries the provider that
/// actually served it (<c>ProviderQuote.ProviderName</c> → recorded on the
/// SpotPrice row). Only the poller calls this class; request paths read the
/// PriceCache.
/// </summary>
public sealed class CompositeMetalPriceProvider : IMetalPriceProvider
{
    private readonly IReadOnlyList<IMetalPriceProvider> _chain;
    private readonly ILogger<CompositeMetalPriceProvider> _logger;

    /// <summary>Initializes the chain (configured order: primary then fallback).</summary>
    public CompositeMetalPriceProvider(IEnumerable<IMetalPriceProvider> chain, ILogger<CompositeMetalPriceProvider> logger)
    {
        _chain = [.. chain];
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProviderQuote> GetCurrentAsync(MetalKind metal, Currency currency, CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();
        foreach (var provider in _chain)
        {
            try
            {
                return await provider.GetCurrentAsync(metal, currency, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException or InvalidOperationException)
            {
                failures.Add($"{provider.GetType().Name}: {ex.Message}");
                _logger.LogWarning("Spot provider {Provider} failed for {Metal} {Currency}: {Message}", provider.GetType().Name, metal, currency, ex.Message);
            }
        }

        throw new InvalidOperationException($"All spot price providers failed. {string.Join(" | ", failures)}");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DailySpotQuote>> GetDailyHistoryAsync(
        MetalKind metal,
        Currency currency,
        DateRange range,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();
        foreach (var provider in _chain)
        {
            try
            {
                return await provider.GetDailyHistoryAsync(metal, currency, range, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException or InvalidOperationException)
            {
                failures.Add($"{provider.GetType().Name}: {ex.Message}");
                _logger.LogWarning("History provider {Provider} failed for {Metal} {Currency}: {Message}", provider.GetType().Name, metal, currency, ex.Message);
            }
        }

        throw new InvalidOperationException($"All spot price providers failed for history. {string.Join(" | ", failures)}");
    }
}
