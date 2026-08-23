using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mintmark.Application.Ports;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Domain.ValueObjects;
using Mintmark.Infrastructure.Identification;
using Mintmark.Infrastructure.Persistence;
using Mintmark.Infrastructure.Storage;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using CoinSide = Mintmark.Domain.CoinSide;

namespace Mintmark.Infrastructure.Seed;

/// <summary>Shape of backend/seed/catalog.json (the researched catalog; used verbatim).</summary>
public sealed class CatalogFile
{
    /// <summary>Gets or sets the mints.</summary>
    [JsonPropertyName("mints")]
    public List<MintRow> Mints { get; set; } = [];

    /// <summary>Gets or sets the series.</summary>
    [JsonPropertyName("series")]
    public List<SeriesRow> Series { get; set; } = [];

    /// <summary>Gets or sets the coin types.</summary>
    [JsonPropertyName("coinTypes")]
    public List<CoinTypeRow> CoinTypes { get; set; } = [];

    /// <summary>One mint.</summary>
    public sealed class MintRow
    {
        /// <summary>Gets or sets the display name.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the country.</summary>
        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;

        /// <summary>Gets or sets the two-letter ISO code.</summary>
        [JsonPropertyName("isoCode")]
        public string IsoCode { get; set; } = string.Empty;

        /// <summary>gets or sets the mint marks.</summary>
        [JsonPropertyName("mintMarks")]
        public List<string>? MintMarks { get; set; }

        /// <summary>Gets or sets the founding year.</summary>
        [JsonPropertyName("foundedYear")]
        public int? FoundedYear { get; set; }

        /// <summary>Gets or sets the active flag.</summary>
        [JsonPropertyName("active")]
        public bool Active { get; set; } = true;

        /// <summary>Gets or sets the source URL.</summary>
        [JsonPropertyName("sourceUrl")]
        public string? SourceUrl { get; set; }
    }

    /// <summary>One series.</summary>
    public sealed class SeriesRow
    {
        /// <summary>Gets or sets the display name.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the issuing mint by name (null for generic series).</summary>
        [JsonPropertyName("mintName")]
        public string? MintName { get; set; }

        /// <summary>Gets or sets the metal.</summary>
        [JsonPropertyName("metal")]
        public string Metal { get; set; } = "silver";

        /// <summary>Gets or sets the first year.</summary>
        [JsonPropertyName("startYear")]
        public int? StartYear { get; set; }

        /// <summary>Gets or sets the last year.</summary>
        [JsonPropertyName("endYear")]
        public int? EndYear { get; set; }

        /// <summary>Gets or sets the source URL.</summary>
        [JsonPropertyName("sourceUrl")]
        public string? SourceUrl { get; set; }
    }

    /// <summary>One coin type (spec figures; null means unverified — never filled in).</summary>
    public sealed class CoinTypeRow
    {
        /// <summary>Gets or sets the owning series by name.</summary>
        [JsonPropertyName("seriesName")]
        public string SeriesName { get; set; } = string.Empty;

        /// <summary>Gets or sets the year of issue.</summary>
        [JsonPropertyName("year")]
        public int? Year { get; set; }

        /// <summary>gets or sets the mint mark.</summary>
        [JsonPropertyName("mintMark")]
        public string? MintMark { get; set; }

        /// <summary>Gets or sets the denomination.</summary>
        [JsonPropertyName("denomination")]
        public string? Denomination { get; set; }

        /// <summary>Gets or sets the metal.</summary>
        [JsonPropertyName("metal")]
        public string? Metal { get; set; }

        /// <summary>Gets or sets the fineness.</summary>
        [JsonPropertyName("fineness")]
        public decimal? Fineness { get; set; }

        /// <summary>Gets or sets the gross weight in grams.</summary>
        [JsonPropertyName("grossWeightGrams")]
        public decimal? GrossWeightGrams { get; set; }

        /// <summary>Gets or sets the actual metal weight in troy ounces.</summary>
        [JsonPropertyName("actualMetalWeightTroyOz")]
        public decimal? ActualMetalWeightTroyOz { get; set; }

        /// <summary>Gets or sets the diameter in mm.</summary>
        [JsonPropertyName("diameterMm")]
        public decimal? DiameterMm { get; set; }

        /// <summary>Gets or sets the thickness in mm.</summary>
        [JsonPropertyName("thicknessMm")]
        public decimal? ThicknessMm { get; set; }

        /// <summary>Gets or sets the edge description.</summary>
        [JsonPropertyName("edge")]
        public string? Edge { get; set; }

        /// <summary>Gets or sets the mintage figure.</summary>
        [JsonPropertyName("mintage")]
        public long? Mintage { get; set; }

        /// <summary>Gets or sets the mintage source URL.</summary>
        [JsonPropertyName("mintageSourceUrl")]
        public string? MintageSourceUrl { get; set; }

        /// <summary>Gets or sets the primary finish.</summary>
        [JsonPropertyName("finishPrimary")]
        public string? FinishPrimary { get; set; }

        /// <summary>Gets or sets the spec source URL (required when specs are present).</summary>
        [JsonPropertyName("sourceUrl")]
        public string? SourceUrl { get; set; }
    }
}

/// <summary>
/// Loads backend/seed/catalog.json verbatim into the catalog tables,
/// idempotently. Seed-file validation enforces the domain hard rule: a row
/// carrying any specification figure MUST carry a source URL. Rows with no
/// figures at all (the generic silver round) seed their series only — the
/// bound CoinType factory requires fineness/weight, and unverifiable figures
/// stay null rather than invented.
/// </summary>
public sealed class CatalogSeeder(
    MintmarkDbContext dbContext,
    IImageStore imageStore,
    ILogger<CatalogSeeder> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Seeds mints, series, coin types, demand tiers and placeholder reference images.</summary>
    public async Task SeedAsync(string catalogPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);

        await using var stream = File.OpenRead(catalogPath);
        var catalog = await JsonSerializer.DeserializeAsync<CatalogFile>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Catalog file {catalogPath} is empty or malformed.");

        var mints = await SeedMintsAsync(catalog, cancellationToken);
        var series = await SeedSeriesAsync(catalog, mints, cancellationToken);
        await SeedCoinTypesAsync(catalog, series, mints, cancellationToken);
    }

    private async Task<Dictionary<string, Mint>> SeedMintsAsync(CatalogFile catalog, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Mints.ToDictionaryAsync(m => m.Name, m => m, cancellationToken);
        foreach (var row in catalog.Mints)
        {
            if (existing.TryGetValue(row.Name, out _))
            {
                continue;
            }

            var mint = Mint.Create(
                row.Name,
                row.Country,
                row.IsoCode,
                row.MintMarks,
                row.FoundedYear,
                row.Active,
                notes: row.SourceUrl is null ? null : $"source: {row.SourceUrl}");
            dbContext.Mints.Add(mint);
            existing[row.Name] = mint;
        }

        // Seeder-derived (not in catalog.json): the series list references
        // "South African Mint" for the silver Krugerrand. Facts from
        // samint.co.za: founded 1941, South Africa.
        if (!existing.ContainsKey("South African Mint"))
        {
            var saMint = Mint.Create(
                "South African Mint",
                "South Africa",
                "ZA",
                mintMarks: [],
                foundedYear: 1941,
                isActive: true,
                notes: "seeder-derived from https://www.samint.co.za/2023-range/");
            dbContext.Mints.Add(saMint);
            existing["South African Mint"] = saMint;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private async Task<Dictionary<string, Series>> SeedSeriesAsync(
        CatalogFile catalog,
        Dictionary<string, Mint> mints,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Series.ToDictionaryAsync(s => s.Name, s => s, cancellationToken);
        foreach (var row in catalog.Series)
        {
            if (existing.ContainsKey(row.Name))
            {
                continue;
            }

            MintId? mintId = null;
            if (row.MintName is not null)
            {
                var mint = ResolveMint(row.MintName, mints)
                    ?? throw new InvalidOperationException($"Series '{row.Name}' references unknown mint '{row.MintName}'.");
                mintId = mint.Id;
            }

            // A series needs a mint to be joinable in hybrid search; generic
            // series (null mint) are skipped as catalog rows — they describe
            // uncataloged holdings, which never get a CoinType either.
            if (mintId is null)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Series '{Series}' has no mint (generic); not seeded as a catalog series.", row.Name);
                }
                continue;
            }

            var series = Series.Create(
                row.Name,
                mintId.Value,
                ParseMetal(row.Metal),
                row.StartYear,
                row.EndYear,
                row.SourceUrl is null ? null : $"source: {row.SourceUrl}");
            dbContext.Series.Add(series);
            existing[row.Name] = series;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // ADR 0007 demand tier per series is reference data; Medium is the
        // neutral default until curated tiers exist (docs/open-questions.md).
        foreach (var series in existing.Values)
        {
            if (!await dbContext.SeriesDemandTiers.AnyAsync(t => t.SeriesId == series.Id, cancellationToken))
            {
                dbContext.SeriesDemandTiers.Add(new SeriesDemandTierRow
                {
                    SeriesId = series.Id,
                    Tier = SeriesDemandTier.Medium,
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private async Task SeedCoinTypesAsync(
        CatalogFile catalog,
        Dictionary<string, Series> series,
        Dictionary<string, Mint> mints,
        CancellationToken cancellationToken)
    {
        foreach (var row in catalog.CoinTypes)
        {
            if (!series.TryGetValue(row.SeriesName, out var owningSeries))
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("CoinType row for '{Series}' skipped: series not seeded (generic).", row.SeriesName);
                }
                continue;
            }

            var hasSpecs = row.Fineness is not null
                || row.GrossWeightGrams is not null
                || row.ActualMetalWeightTroyOz is not null
                || row.DiameterMm is not null
                || row.ThicknessMm is not null
                || row.Mintage is not null;

            // Hard rule: specs without a source URL are rejected outright.
            if (hasSpecs && string.IsNullOrWhiteSpace(row.SourceUrl))
            {
                throw new InvalidOperationException(
                    $"Catalog row '{row.SeriesName} {row.Year}' carries specification figures without a source URL — rejected (domain hard rule).");
            }

            // No figures and no year: a placeholder series row (the generic
            // silver round). Nothing to catalog.
            if (!hasSpecs && row.Year is null)
            {
                continue;
            }

            if (row.Year is null || row.Fineness is null || row.ActualMetalWeightTroyOz is null)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation(
                        "CoinType '{Series} {Year}' skipped: incomplete figures stay null by the research (no CoinType row).",
                        row.SeriesName, row.Year);
                }
                continue;
            }

            // The bound CoinType factory requires a gross weight; where the
            // research left it null but published AMW + fineness exist, the
            // gross weight is DERIVED (AMW grams / fineness). This is the
            // same derivation the research itself applied to the ASE row;
            // see docs/open-questions.md.
            var grossWeight = row.GrossWeightGrams
                ?? Math.Round(row.ActualMetalWeightTroyOz.Value * Weight.GramsPerTroyOunce / row.Fineness.Value, 3);

            var name = ComposeName(row);
            var duplicate = await dbContext.CoinTypes.AnyAsync(
                c => c.SeriesId == owningSeries.Id && c.Year == row.Year && c.Name == name,
                cancellationToken);
            if (duplicate)
            {
                continue;
            }

            var finish = ParseFinish(row.FinishPrimary);

            // Reference-image keys are id-independent so they can be set at
            // creation (CoinType.Create takes them); the placeholder PNGs are
            // generated right after the row exists.
            var slug = Slug($"{row.SeriesName}-{row.Year}-{row.FinishPrimary}-{row.MintMark}");
            var obverseKey = $"reference/{owningSeries.Id.Value}/{slug}-obverse.png";
            var reverseKey = $"reference/{owningSeries.Id.Value}/{slug}-reverse.png";

            var coinType = CoinType.Create(
                owningSeries.Id,
                ResolveMint(row, owningSeries, mints).Id,
                row.Year.Value,
                name,
                finish,
                row.Fineness.Value,
                grossWeight,
                row.ActualMetalWeightTroyOz.Value,
                row.SourceUrl!,
                finishAttributes: FinishAttribute.None,
                mintage: row.Mintage,
                diameterMillimeters: row.DiameterMm,
                thicknessMillimeters: row.ThicknessMm,
                edge: ParseEdge(row.Edge),
                obverseImageKey: obverseKey,
                reverseImageKey: reverseKey);
            dbContext.CoinTypes.Add(coinType);
            await dbContext.SaveChangesAsync(cancellationToken);

            await SeedReferenceImagesAsync(coinType, obverseKey, reverseKey, cancellationToken);
        }
    }

    private async Task SeedReferenceImagesAsync(
        CoinType coinType,
        string obverseKey,
        string reverseKey,
        CancellationToken cancellationToken)
    {
        if (await dbContext.ReferenceImages.AnyAsync(r => r.CoinTypeId == coinType.Id, cancellationToken))
        {
            return;
        }

        foreach (var (side, key, isObverse) in new[]
                 {
                     (CoinSide.Obverse, obverseKey, true),
                     (CoinSide.Reverse, reverseKey, false),
                 })
        {
            var png = PlaceholderImageGenerator.Generate(coinType.Name, isObverse);
            var hash = PerceptualHasher.Hash(png);
            await imageStore.SaveAsync(key, png, "image/png", cancellationToken);

            dbContext.ReferenceImages.Add(new ReferenceImage
            {
                CoinTypeId = coinType.Id,
                Side = side,
                StorageKey = key,
                PerceptualHash = hash,
                Embedding = new Pgvector.Vector(EmbeddingService.FromHash(hash)),
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Mint-name resolution: exact first, then normalized (parentheticals
    /// stripped, diacritics folded, case-insensitive) — catalog.json names
    /// series mints slightly differently in two places (e.g.
    /// "Münze Österreich" vs "Münze Österreich (Austrian Mint)").
    /// </summary>
    private static Mint? ResolveMint(string mintName, Dictionary<string, Mint> mints)
    {
        if (mints.TryGetValue(mintName, out var exact))
        {
            return exact;
        }

        var normalized = NormalizeMintName(mintName);
        return mints.Values.FirstOrDefault(m => NormalizeMintName(m.Name) == normalized);
    }

    private static string NormalizeMintName(string name)
    {
        var formD = name.Normalize(System.Text.NormalizationForm.FormD);
        var stripped = new string(formD.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
            != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        var withoutParens = System.Text.RegularExpressions.Regex.Replace(stripped, @"\(.*?\)", string.Empty).Trim();
        return withoutParens.Trim().ToLowerInvariant();
    }

    private static Mint ResolveMint(CatalogFile.CoinTypeRow row, Series owningSeries, Dictionary<string, Mint> mints)
    {
        // One mint entity carries all of a country's marks (the US Mint row
        // holds P/D/S/W), so the series' mint strikes every row; a foreign
        // mark that maps to another seeded mint wins (Mo -> Casa de Moneda).
        if (!string.IsNullOrWhiteSpace(row.MintMark))
        {
            var byMark = mints.Values.FirstOrDefault(m => m.MintMarks.Contains(row.MintMark.Trim().ToUpperInvariant()));
            if (byMark is not null)
            {
                return byMark;
            }
        }

        return mints.Values.First(m => m.Id == owningSeries.MintId);
    }

    private static string ComposeName(CatalogFile.CoinTypeRow row) =>
        $"{row.Year} {row.SeriesName} {row.FinishPrimary ?? "Unknown"}"
            + (string.IsNullOrWhiteSpace(row.MintMark) ? string.Empty : $" ({row.MintMark.Trim().ToUpperInvariant()})")
            + (string.IsNullOrWhiteSpace(row.Denomination) ? string.Empty : $" {row.Denomination}");

    private static string Slug(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        return new string(chars).Trim('-');
    }

    private static MetalKind ParseMetal(string metal) => metal.Trim().ToLowerInvariant() switch
    {
        "gold" => MetalKind.Gold,
        "silver" => MetalKind.Silver,
        "platinum" => MetalKind.Platinum,
        "palladium" => MetalKind.Palladium,
        _ => throw new InvalidOperationException($"Unknown metal '{metal}' in catalog.json."),
    };

    private static FinishPrimary ParseFinish(string? finish) => finish?.Trim() switch
    {
        "BusinessStrike" => FinishPrimary.BusinessStrike,
        "BullionUncirculated" => FinishPrimary.BullionUncirculated,
        "Proof" => FinishPrimary.Proof,
        "ReverseProof" => FinishPrimary.ReverseProof,
        "Burnished" => FinishPrimary.Burnished,
        "MatteProof" => FinishPrimary.MatteProof,
        null or "" => FinishPrimary.Unknown,
        _ => FinishPrimary.Unknown,
    };

    private static EdgeType? ParseEdge(string? edge) => edge?.Trim().ToLowerInvariant() switch
    {
        "reeded" => EdgeType.Reeded,
        "serrated" => EdgeType.Reeded, // closest domain value; see docs/open-questions.md
        "lettered" => EdgeType.Lettered,
        "plain" => EdgeType.Plain,
        null or "" => null,
        _ => null,
    };
}
