using Mintmark.Application.Ports;

namespace Mintmark.Application.UseCases;

/// <summary>
/// Thin catalog-search orchestration: normalizes the query (trims empty
/// filters, caps the limit at the top-5 contract) and delegates to the
/// hybrid-search port.
/// </summary>
public sealed class CatalogSearchService
{
    /// <summary>The maximum number of candidates a search returns.</summary>
    public const int MaxCandidates = 5;

    private readonly ICoinSearch _coinSearch;

    /// <summary>Initializes the service with its search port.</summary>
    public CatalogSearchService(ICoinSearch coinSearch) => _coinSearch = coinSearch;

    /// <summary>Runs a normalized hybrid search and returns the top candidates, best first.</summary>
    public Task<IReadOnlyList<CoinCandidate>> SearchAsync(
        CoinSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalized = query with
        {
            FreeText = Normalize(query.FreeText),
            Country = Normalize(query.Country),
            Series = Normalize(query.Series),
            Limit = Math.Clamp(query.Limit, 1, MaxCandidates),
        };

        return _coinSearch.SearchAsync(normalized, cancellationToken);
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
