using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Mintmark.Application.Ports;
using Mintmark.Domain;
using Mintmark.Infrastructure.Persistence;
using Npgsql;

namespace Mintmark.Infrastructure.Identification;

/// <summary>Internal projection of one hybrid-search candidate row.</summary>
public sealed record CoinSearchRow(long CoinTypeId, string DisplayName, decimal Score);

/// <summary>
/// Hybrid catalog retrieval over PostgreSQL (architecture.md): perceptual
/// hash Hamming distance on seeded reference images, pgvector cosine when
/// embeddings are present, pg_trgm similarity on the catalog name, and
/// structured filters (year ±2, fineness ±0.05, series/country text) —
/// blended into a top-5 list. Weights: image 45, text 35, structured 20;
/// unavailable components drop out and the denominator renormalizes.
/// Structured components contribute a neutral 0.5 when the query does not
/// constrain them (the bound CoinSearchQuery port carries no metal/AMW
/// fields — see docs/open-questions.md).
/// </summary>
public sealed class HybridCoinSearch(MintmarkDbContext dbContext) : ICoinSearch
{
    private const string Sql = """
        WITH best_image AS (
            SELECT ri.coin_type_id,
                   MAX(
                       CASE
                           WHEN @hash IS NULL AND (ri.embedding IS NULL OR @qvec IS NULL) THEN NULL
                           WHEN @hash IS NULL THEN 1.0 - (ri.embedding <=> @qvec::vector)
                           WHEN ri.embedding IS NULL OR @qvec IS NULL THEN 1.0 - bit_count((ri.perceptual_hash # @hash)::bit(64)) / 64.0
                           ELSE (1.0 - bit_count((ri.perceptual_hash # @hash)::bit(64)) / 64.0 + 1.0 - (ri.embedding <=> @qvec::vector)) / 2.0
                       END
                   ) AS img_score
            FROM reference_images ri
            GROUP BY ri.coin_type_id
        )
        SELECT ct.id   AS "CoinTypeId",
               ct.name AS "DisplayName",
               (
               (
                 45.0 * COALESCE(bi.img_score, 0.0)
                 + 35.0 * similarity(ct.name, @freetext)
                 + 20.0 * (
                     (CASE WHEN @year IS NULL THEN 0.5 WHEN ABS(ct.year - @year) <= 2 THEN 1.0 ELSE 0.0 END)
                   + (CASE WHEN @fineness IS NULL THEN 0.5 WHEN ABS(ct.fineness - @fineness) <= 0.05 THEN 1.0 ELSE 0.0 END)
                   + (CASE WHEN @series IS NULL THEN 0.5 WHEN s.name ILIKE '%' || @series || '%' THEN 1.0 ELSE 0.0 END)
                   + (CASE WHEN @country IS NULL THEN 0.5 WHEN m.country ILIKE '%' || @country || '%' THEN 1.0 ELSE 0.0 END)
                 ) / 4.0
               ) / (
                 45.0 * (CASE WHEN bi.img_score IS NULL THEN 0.0 ELSE 1.0 END) + 35.0 + 20.0
               ) )::numeric(9,6) AS "Score"
        FROM coin_types ct
        JOIN series s ON s.id = ct.series_id
        JOIN mints m ON m.id = ct.mint_id
        LEFT JOIN best_image bi ON bi.coin_type_id = ct.id
        ORDER BY "Score" DESC, ct.id
        LIMIT @limit
        """;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CoinCandidate>> SearchAsync(CoinSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        long? hash = query.PerceptualHash is { } h ? unchecked((long)h) : null;
        string? qvec = hash is null
            ? null
            : "[" + string.Join(",", EmbeddingService.FromHash(query.PerceptualHash!.Value)
                .Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + "]";

        var parameters = new object[]
        {
            new NpgsqlParameter<long?>("hash", hash),
            new NpgsqlParameter<string?>("qvec", qvec),
            new NpgsqlParameter<string>("freetext", query.FreeText ?? string.Empty),
            new NpgsqlParameter<int?>("year", query.Year),
            new NpgsqlParameter<decimal?>("fineness", query.Fineness),
            new NpgsqlParameter<string?>("series", query.Series),
            new NpgsqlParameter<string?>("country", query.Country),
            new NpgsqlParameter<int>("limit", query.Limit),
        };

        var rows = await dbContext.Database
            .SqlQueryRaw<CoinSearchRow>(Sql, parameters)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new CoinCandidate(new CoinTypeId(r.CoinTypeId), decimal.Round(r.Score, 4), r.DisplayName))];
    }
}
