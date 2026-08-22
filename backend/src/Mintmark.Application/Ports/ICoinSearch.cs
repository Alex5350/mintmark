using Mintmark.Domain;

namespace Mintmark.Application.Ports;

/// <summary>
/// A hybrid catalog search query: vector similarity (embedding of the query
/// image), trigram text similarity, and structured filters, blended by the
/// implementation. Defaults to the top 5 candidates.
/// </summary>
public sealed record CoinSearchQuery
{
    /// <summary>Initializes the query.</summary>
    public CoinSearchQuery()
    {
    }

    /// <summary>Initializes the query with free text.</summary>
    public CoinSearchQuery(string? freeText)
        : this(freeText, null, null, null, null, null, 5)
    {
    }

    /// <summary>Initializes the query.</summary>
    public CoinSearchQuery(
        string? freeText,
        string? country,
        string? series,
        int? year,
        decimal? fineness,
        ulong? perceptualHash,
        int limit)
    {
        FreeText = freeText;
        Country = country;
        Series = series;
        Year = year;
        Fineness = fineness;
        PerceptualHash = perceptualHash;
        Limit = limit is < 1 or > 5 ? 5 : limit;
    }

    /// <summary>Gets free-text trigram input (e.g. "2023 libertad 2oz").</summary>
    public string? FreeText { get; init; }

    /// <summary>Gets the country filter, when known.</summary>
    public string? Country { get; init; }

    /// <summary>Gets the series filter, when known.</summary>
    public string? Series { get; init; }

    /// <summary>Gets the year filter, when known.</summary>
    public int? Year { get; init; }

    /// <summary>Gets the fineness filter, when known.</summary>
    public decimal? Fineness { get; init; }

    /// <summary>Gets the query image's perceptual hash for near-duplicate matching.</summary>
    public ulong? PerceptualHash { get; init; }

    /// <summary>Gets the maximum number of candidates to return (1..5, default 5).</summary>
    public int Limit { get; init; } = 5;
}

/// <summary>A hybrid-search candidate: catalog row, blended score, display label.</summary>
/// <param name="CoinTypeId">The matched catalog row.</param>
/// <param name="Score">The blended similarity score (higher is better).</param>
/// <param name="DisplayName">A human-readable label for UI lists.</param>
public sealed record CoinCandidate(CoinTypeId CoinTypeId, decimal Score, string DisplayName);

/// <summary>
/// Port to catalog search over CoinType (vector + trigram + filters).
/// Implemented by Infrastructure against PostgreSQL/pgvector + pg_trgm.
/// </summary>
public interface ICoinSearch
{
    /// <summary>Runs a hybrid search and returns the top candidates.</summary>
    Task<IReadOnlyList<CoinCandidate>> SearchAsync(CoinSearchQuery query, CancellationToken cancellationToken = default);
}
