using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Mintmark.Application.Ports;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Infrastructure.Persistence;
using Mintmark.Infrastructure.Storage;
using Npgsql;

namespace Mintmark.Infrastructure.Identification;

/// <summary>A seeded reference image matched by perceptual hash, with its catalog row joined in.</summary>
/// <param name="CoinTypeId">Matched catalog row.</param>
/// <param name="CoinTypeName">Catalog display name.</param>
/// <param name="Year">Year of issue.</param>
/// <param name="SeriesName">Series name.</param>
/// <param name="Metal">Series metal.</param>
/// <param name="Fineness">Fineness.</param>
/// <param name="Finish">Primary finish.</param>
/// <param name="MintName">Mint name.</param>
/// <param name="Country">Mint country.</param>
/// <param name="HammingDistance">Hamming distance between the query hash and the reference hash.</param>
/// Raw-SQL projection. Plain settable properties with primitive types:
/// EF's SqlQuery materialization binds by property name and cannot bind
/// typed-ID constructor parameters or enum conversions here.
public sealed class OfflineReferenceMatch
{
    public long CoinTypeId { get; init; }
    public string CoinTypeName { get; init; } = "";
    public int Year { get; init; }
    public string SeriesName { get; init; } = "";
    public string Metal { get; init; } = "";
    public decimal Fineness { get; init; }
    public string Finish { get; init; } = "";
    public string MintName { get; init; } = "";
    public string Country { get; init; } = "";
    public int HammingDistance { get; init; }
}

/// <summary>
/// Finds the nearest seeded reference image by pHash Hamming distance
/// (bit_count of XOR over bigint hashes — PostgreSQL 14+).
/// </summary>
public sealed class ReferenceImageMatcher(MintmarkDbContext dbContext)
{
    /// <summary>Finds the best-matching reference for a perceptual hash, or null.</summary>
    public async Task<OfflineReferenceMatch?> FindBestAsync(ulong perceptualHash, int maxHammingDistance, CancellationToken cancellationToken = default)
    {
        var hash = new NpgsqlParameter<long>("hash", unchecked((long)perceptualHash));
        var max = new NpgsqlParameter<int>("max", maxHammingDistance);

        var rows = await dbContext.Database
            .SqlQueryRaw<OfflineReferenceMatch>("""
                SELECT ct.id                                    AS "CoinTypeId",
                       ct.name                                  AS "CoinTypeName",
                       ct.year                                  AS "Year",
                       s.name                                   AS "SeriesName",
                       s.metal                                  AS "Metal",
                       ct.fineness                              AS "Fineness",
                       ct.finish                                AS "Finish",
                       m.name                                   AS "MintName",
                       m.country                                AS "Country",
                       bit_count((ri.perceptual_hash # @hash)::bit(64))    AS "HammingDistance"
                FROM reference_images ri
                JOIN coin_types ct ON ct.id = ri.coin_type_id
                JOIN series s ON s.id = ct.series_id
                JOIN mints m ON m.id = ct.mint_id
                WHERE bit_count((ri.perceptual_hash # @hash)::bit(64)) <= @max
                ORDER BY bit_count((ri.perceptual_hash # @hash)::bit(64))
                LIMIT 1
                """, hash, max)
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }
}

/// <summary>
/// The deterministic offline evaluator behind the vision port (ADR 0009):
/// pHash the normalized obverse, match it against seeded reference hashes,
/// and emit per-field observations with similarity-derived confidence.
/// Every response is labeled with the provider <c>offline</c> — it never
/// claims model inference, never invents a value the match cannot support,
/// and returns honest nulls when nothing matches. The full pipeline runs
/// end-to-end without keys or spend.
/// </summary>
public sealed class OfflineVisionEvaluator(
    IPerceptualHasher hasher,
    ReferenceImageMatcher matcher) : IVisionIdentifier
{
    /// <summary>The provider label recorded on runs (ADR 0009).</summary>
    public const string ProviderLabel = "offline";

    /// <summary>The evaluator version (bump when the matching/confidence rules change).</summary>
    public const string EvaluatorVersion = "phash-match-v1";

    /// <summary>Maximum Hamming distance (of 64 bits) that still counts as a match.</summary>
    public const int MaxHammingDistance = 12;

    /// <summary>Confidence cap: hash similarity is weaker evidence than reading the coin.</summary>
    public const decimal ConfidenceCap = 0.85m;

    /// <inheritdoc />
    public async Task<VisionIdentification> IdentifyAsync(ImageInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var canonical = ImagePreprocessor.Preprocess(input.ObverseBytes);
        var hash = await hasher.HashAsync(canonical, cancellationToken);
        var match = await matcher.FindBestAsync(hash, MaxHammingDistance, cancellationToken);

        var similarity = match is null
            ? 0m
            : decimal.Clamp(1m - (match.HammingDistance / 64m), 0m, 1m);
        var confidence = match is null ? 0m : decimal.Min(ConfidenceCap, 0.40m + (0.60m * similarity));
        var evidence = match is null
            ? null
            : $"offline perceptual-hash match (Hamming {match.HammingDistance}/64) against seeded reference of {match.CoinTypeName}";

        var raw = BuildRawResponse(match, similarity, confidence);
        return new VisionIdentification(
            ProviderLabel,
            EvaluatorVersion,
            raw,
            country: new FieldObservation<string?>(match?.Country, confidence, evidence),
            mint: new FieldObservation<string?>(match?.MintName, confidence, evidence),
            series: new FieldObservation<string?>(match?.SeriesName, confidence, evidence),
            year: new FieldObservation<int?>(match?.Year, confidence, evidence),
            denomination: new FieldObservation<string?>(null, 0m, "offline evaluator: denomination is not part of the reference catalog match"),
            metal: new FieldObservation<string?>(match?.Metal, confidence, evidence),
            fineness: new FieldObservation<decimal?>(match?.Fineness, confidence, evidence),
            sizeEstimateTroyOz: new FieldObservation<decimal?>(null, 0m, "no scale reference visible — never guessed"),
            finish: new FieldObservation<string?>(match?.Finish, confidence, evidence),
            finishAttributes: [],
            edge: new FieldObservation<string?>(null, 0m, "offline evaluator: edge not observable from face photos"),
            conditionNotes: [match is null
                ? "offline evaluator: no reference image within Hamming threshold; no fields inferred"
                : "offline evaluator: condition is not assessed by perceptual-hash matching"],
            authenticityFlags: [],
            imageQualityIssues: []);
    }

    private static string BuildRawResponse(OfflineReferenceMatch? match, decimal similarity, decimal confidence)
    {
        var text = match is null
            ? """{"provider":"offline","matched":false,"similarity":0,"confidence":0}"""
            : string.Create(
                CultureInfo.InvariantCulture,
                $$"""{"provider":"offline","matched":true,"coinTypeId":{{match.CoinTypeId}},"similarity":{{similarity:0.####}},"confidence":{{confidence:0.####}},"hammingDistance":{{match.HammingDistance}}}""");
        return text;
    }
}
