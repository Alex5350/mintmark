using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Infrastructure.Persistence;

/// <summary>
/// The EF Core 10 / Npgsql mapping for every Mintmark aggregate plus the
/// Identity and Infrastructure-owned tables. Mapping decisions (documented
/// once, applied uniformly):
/// <list type="bullet">
/// <item><description>snake_case names for tables/columns/keys/FKs/indexes — via <see cref="SnakeCaseNaming"/> (no maintained convention package exists for EF 10).</description></item>
/// <item><description><see cref="Money"/> maps as an EF complex property: <c>amount numeric(18,4)</c> + <c>currency char(3)</c>. The Domain persists no <see cref="Weight"/> value object directly — CoinType carries its decimal columns by binding.</description></item>
/// <item><description>Typed ids convert to <c>bigint</c> identity columns; the 64-bit perceptual hash converts <c>ulong</c> ↔ <c>bigint</c> (PostgreSQL has no unsigned bigint).</description></item>
/// <item><description>Enums store as text (simpler than Npgsql enum types: readable rows, no per-connection type registration); [Flags] enums store as <c>int</c>.</description></item>
/// <item><description>Row-level auth: a global query filter scopes <see cref="Holding"/> (and everything reached through it) to <see cref="CurrentUserId"/>, combined with soft-delete.</description></item>
/// </list>
/// </summary>
public sealed class MintmarkDbContext(
    DbContextOptions<MintmarkDbContext> options)
    : IdentityDbContext<MintmarkUser, IdentityRole<long>, long>(options)
{
    // Round-trips candidate lists with the SAME typed-id wire form the API
    // emits (plain longs) — otherwise persisted ids re-read as zero.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        Converters = { new Mintmark.Application.TypedIdJsonConverterFactory() },
    };

    /// <summary>
    /// Gets or sets the user whose holdings are visible through the global
    /// query filter. Set per request by the API's user-context middleware;
    /// zero (no user) sees no holdings — the safe default.
    /// </summary>
    public UserId CurrentUserId { get; set; }

    /// <summary>Gets or sets the catalog mints.</summary>
    public DbSet<Mint> Mints { get; set; } = null!;

    /// <summary>Gets or sets the catalog series.</summary>
    public DbSet<Series> Series { get; set; } = null!;

    /// <summary>Gets or sets the catalog coin types.</summary>
    public DbSet<CoinType> CoinTypes { get; set; } = null!;

    /// <summary>Gets or sets the holdings (row-scoped by the global query filter).</summary>
    public DbSet<Holding> Holdings { get; set; } = null!;

    /// <summary>Gets or sets the holding correction revisions.</summary>
    public DbSet<HoldingRevision> HoldingRevisions { get; set; } = null!;

    /// <summary>Gets or sets the optional grading records.</summary>
    public DbSet<Grading> Gradings { get; set; } = null!;

    /// <summary>Gets or sets the holding photos.</summary>
    public DbSet<CoinImage> CoinImages { get; set; } = null!;

    /// <summary>Gets or sets the identification runs.</summary>
    public DbSet<IdentificationRun> IdentificationRuns { get; set; } = null!;

    /// <summary>Gets or sets the spot price ticks.</summary>
    public DbSet<SpotPrice> SpotPrices { get; set; } = null!;

    /// <summary>Gets or sets the daily spot closes.</summary>
    public DbSet<SpotPriceDaily> SpotPriceDaily { get; set; } = null!;

    /// <summary>Gets or sets the valuations.</summary>
    public DbSet<Valuation> Valuations { get; set; } = null!;

    /// <summary>Gets or sets the refresh tokens.</summary>
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    /// <summary>Gets or sets the idempotency replay records.</summary>
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; } = null!;

    /// <summary>Gets or sets the catalog reference images (retrieval corpus).</summary>
    public DbSet<ReferenceImage> ReferenceImages { get; set; } = null!;

    /// <summary>Gets or sets the series demand tiers (ADR 0007 reference data).</summary>
    public DbSet<SeriesDemandTierRow> SeriesDemandTiers { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("vector");
        builder.HasPostgresExtension("pg_trgm");

        ConfigureMint(builder.Entity<Mint>());
        ConfigureSeries(builder.Entity<Series>());
        ConfigureCoinType(builder.Entity<CoinType>());
        ConfigureHolding(builder.Entity<Holding>());
        ConfigureHoldingRevision(builder.Entity<HoldingRevision>());
        ConfigureGrading(builder.Entity<Grading>());
        ConfigureCoinImage(builder.Entity<CoinImage>());
        ConfigureIdentificationRun(builder.Entity<IdentificationRun>());
        ConfigureSpotPrice(builder.Entity<SpotPrice>());
        ConfigureSpotPriceDaily(builder.Entity<SpotPriceDaily>());
        ConfigureValuation(builder.Entity<Valuation>());
        ConfigureRefreshToken(builder.Entity<RefreshToken>());
        ConfigureIdempotencyRecord(builder.Entity<IdempotencyRecord>());
        ConfigureReferenceImage(builder.Entity<ReferenceImage>());
        ConfigureSeriesDemandTier(builder.Entity<SeriesDemandTierRow>());

        // Applies to every entity, Identity tables included; must run last.
        SnakeCaseNaming.Apply(builder);
    }

    private static void ConfigureMint(EntityTypeBuilder<Mint> builder)
    {
        builder.ToTable("mints");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasConversion(Conversions.MintId)
            .UseIdentityByDefaultColumn();
        builder.Property(m => m.Name).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Country).HasMaxLength(100).IsRequired();
        builder.Property(m => m.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(m => m.MintMarks);
        builder.HasIndex(m => m.Name);
    }

    private static void ConfigureSeries(EntityTypeBuilder<Series> builder)
    {
        builder.ToTable("series");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(Conversions.SeriesId)
            .UseIdentityByDefaultColumn();
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Metal).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(s => s.Name);

        builder.HasOne<Mint>()
            .WithMany()
            .HasForeignKey(s => s.MintId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureCoinType(EntityTypeBuilder<CoinType> builder)
    {
        builder.ToTable("coin_types");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(Conversions.CoinTypeId)
            .UseIdentityByDefaultColumn();
        builder.Property(c => c.Name).HasMaxLength(300).IsRequired();
        builder.Property(c => c.Finish).HasConversion<string>().HasMaxLength(32);
        builder.Property(c => c.FinishAttributes).HasConversion<int>();
        builder.Property(c => c.Fineness).HasColumnType("numeric(6,4)");
        builder.Property(c => c.GrossWeightGrams).HasColumnType("numeric(18,6)");
        builder.Property(c => c.ActualMetalWeightTroyOz).HasColumnType("numeric(18,6)");
        builder.Property(c => c.DiameterMillimeters).HasColumnType("numeric(8,3)");
        builder.Property(c => c.ThicknessMillimeters).HasColumnType("numeric(8,3)");
        builder.Property(c => c.Edge).HasConversion<string>().HasMaxLength(16);
        builder.Property(c => c.KmNumber).HasMaxLength(32);
        builder.Property(c => c.RedBookReference).HasMaxLength(64);
        builder.Property(c => c.SourceUrl).HasMaxLength(500).IsRequired();

        // Catalog retrieval: pg_trgm GIN over the catalog name (the bound
        // Domain has no separate legend/denomination columns — the name is
        // the trigram text; see docs/open-questions.md).
        builder.HasIndex(c => c.Name).HasMethod("gin").HasOperators("gin_trgm_ops");
        builder.HasIndex(c => new { c.SeriesId, c.Year });

        builder.HasOne<Series>()
            .WithMany()
            .HasForeignKey(c => c.SeriesId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Mint>()
            .WithMany()
            .HasForeignKey(c => c.MintId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureHolding(EntityTypeBuilder<Holding> builder)
    {
        builder.ToTable("holdings");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id)
            .HasConversion(Conversions.HoldingId)
            .UseIdentityByDefaultColumn();
        builder.Property(h => h.UserId).HasConversion(Conversions.UserId);
        builder.Property(h => h.CoinTypeId).HasConversion(Conversions.CoinTypeId);
        builder.Property(h => h.Form).HasConversion<string>().HasMaxLength(16);
        builder.Property(h => h.Dealer).HasMaxLength(200);
        builder.Property(h => h.StorageLocation).HasMaxLength(200);
        builder.Property(h => h.SerialNumber).HasMaxLength(100);
        builder.Property(h => h.PackagingState).HasMaxLength(100);
        builder.Property(h => h.Notes).HasMaxLength(2000);
        builder.Property(h => h.PurchasedAtUtc);

        builder.ComplexProperty(h => h.PurchasePricePerUnit, money =>
        {
            money.Property(m => m.Amount).HasColumnType("numeric(18,4)");
            money.Property(m => m.Currency)
                .HasConversion(Conversions.Currency)
                .HasColumnType("char(3)");
        });

        builder.HasMany(h => h.Revisions)
            .WithOne()
            .HasForeignKey("HoldingId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(h => h.Revisions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<CoinType>()
            .WithMany()
            .HasForeignKey(h => h.CoinTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.UserId, h.IsDeleted });

        // Row-level authorization: every query (and every navigation reached
        // through a filtered root) is scoped to the current user, and soft
        // deletes never surface. Scoping is enforced here — at the data layer —
        // so no endpoint can forget it.
        builder.HasQueryFilter(h => h.UserId == CurrentUserId && !h.IsDeleted);
    }

    private static void ConfigureHoldingRevision(EntityTypeBuilder<HoldingRevision> builder)
    {
        builder.ToTable("holding_revisions");
        // HoldingId is a shadow FK: the entity is reachable only via Holding.Revisions.
        builder.HasKey("HoldingId", nameof(HoldingRevision.RevisionNumber));
        builder.Property(r => r.Reason).HasMaxLength(500).IsRequired();

        builder.ComplexProperty(r => r.PurchasePricePerUnit, money =>
        {
            money.Property(m => m.Amount).HasColumnType("numeric(18,4)");
            money.Property(m => m.Currency)
                .HasConversion(Conversions.Currency)
                .HasColumnType("char(3)");
        });
    }

    private static void ConfigureGrading(EntityTypeBuilder<Grading> builder)
    {
        builder.ToTable("gradings");
        builder.HasKey(g => g.HoldingId);
        builder.Property(g => g.HoldingId).HasConversion(Conversions.HoldingId);
        builder.Property(g => g.Service).HasConversion<string>().HasMaxLength(16);
        builder.Property(g => g.Designations).HasConversion<int>();
        builder.Property(g => g.CertificationNumber).HasMaxLength(64);
        builder.Property(g => g.LabelPedigree).HasMaxLength(100);
        builder.Property(g => g.VerificationUrl).HasMaxLength(500);

        builder.HasOne<Holding>()
            .WithOne()
            .HasForeignKey<Grading>(g => g.HoldingId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureCoinImage(EntityTypeBuilder<CoinImage> builder)
    {
        builder.ToTable("coin_images");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasConversion(Conversions.ImageId)
            .UseIdentityByDefaultColumn();
        builder.Property(i => i.HoldingId).HasConversion(Conversions.HoldingId);
        builder.Property(i => i.Side).HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.PerceptualHash).HasConversion(Conversions.PerceptualHash);
        builder.Property(i => i.ContentType).HasMaxLength(64);
        builder.Property(i => i.Notes).HasMaxLength(500);

        builder.HasOne<Holding>()
            .WithMany()
            .HasForeignKey(i => i.HoldingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.HoldingId);
    }

    private static void ConfigureIdentificationRun(EntityTypeBuilder<IdentificationRun> builder)
    {
        builder.ToTable("identification_runs");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(Conversions.IdentificationRunId)
            .UseIdentityByDefaultColumn();
        builder.Property(r => r.UserId).HasConversion(Conversions.UserId);
        builder.Property(r => r.ObverseImageId).HasConversion(Conversions.ImageId);
        builder.Property(r => r.ReverseImageId).HasConversion(Conversions.ImageId);
        builder.Property(r => r.EdgeImageId).HasConversion(Conversions.ImageId);
        builder.Property(r => r.ConfirmedCoinTypeId).HasConversion(Conversions.CoinTypeId);
        builder.Property(r => r.ObversePerceptualHash).HasConversion(Conversions.PerceptualHash);
        builder.Property(r => r.ModelName).HasMaxLength(100).IsRequired();
        builder.Property(r => r.ModelVersion).HasMaxLength(50).IsRequired();
        builder.Property(r => r.PromptTemplateVersion).HasMaxLength(50).IsRequired();
        builder.Property(r => r.RawResponse);

        builder.Property(r => r.Candidates)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                s => JsonSerializer.Deserialize<List<IdentificationCandidate>>(s, JsonOptions) ?? new List<IdentificationCandidate>())
            .HasColumnType("jsonb")
            .Metadata.SetValueComparer(Conversions.ListComparer<IdentificationCandidate>());

        // Dictionary<string, decimal> maps to a jsonb object.
        builder.Property(r => r.FieldConfidences)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                s => JsonSerializer.Deserialize<Dictionary<string, decimal>>(s, JsonOptions)
                    ?? new Dictionary<string, decimal>())
            .HasColumnType("jsonb");

        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.ObversePerceptualHash);
    }

    private static void ConfigureSpotPrice(EntityTypeBuilder<SpotPrice> builder)
    {
        builder.ToTable("spot_prices");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(Conversions.SpotPriceId)
            .UseIdentityByDefaultColumn();
        builder.Property(p => p.Metal).HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.Currency).HasConversion(Conversions.Currency).HasColumnType("char(3)");
        builder.Property(p => p.ProviderName).HasMaxLength(64).IsRequired();

        ConfigureMoney(builder.ComplexProperty(p => p.PricePerTroyOunce));
        ConfigureMoney(builder.ComplexProperty(p => p.BidPerTroyOunce));
        ConfigureMoney(builder.ComplexProperty(p => p.AskPerTroyOunce));

        builder.HasIndex(p => new { p.Metal, p.Currency, p.SourceTimestampUtc })
            .IsDescending(false, false, true);
    }

    private static void ConfigureMoney(ComplexPropertyBuilder<Money> money)
    {
        money.Property(m => m.Amount).HasColumnType("numeric(18,4)");
        money.Property(m => m.Currency)
            .HasConversion(Conversions.Currency)
            .HasColumnType("char(3)");
    }

    private static void ConfigureSpotPriceDaily(EntityTypeBuilder<SpotPriceDaily> builder)
    {
        builder.ToTable("spot_price_daily");
        builder.HasKey(d => new { d.Metal, d.Currency, d.Date });
        builder.Property(d => d.Metal).HasConversion<string>().HasMaxLength(16);
        builder.Property(d => d.Currency).HasConversion(Conversions.Currency).HasColumnType("char(3)");
        builder.Property(d => d.ProviderName).HasMaxLength(64);

        ConfigureMoney(builder.ComplexProperty(d => d.Close));
    }

    private static void ConfigureValuation(EntityTypeBuilder<Valuation> builder)
    {
        builder.ToTable("valuations");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id)
            .HasConversion(Conversions.ValuationId)
            .UseIdentityByDefaultColumn();
        builder.Property(v => v.HoldingId).HasConversion(Conversions.HoldingId);
        builder.Property(v => v.DerivedFromSpotPriceId).HasConversion(Conversions.SpotPriceId);
        builder.Property(v => v.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(v => v.SpotProviderName).HasMaxLength(64).IsRequired();
        builder.Property(v => v.Method).HasMaxLength(64).IsRequired();
        builder.Property(v => v.MethodVersion).HasMaxLength(32).IsRequired();
        builder.Property(v => v.ConfidenceBandLow).HasColumnType("numeric(6,4)");
        builder.Property(v => v.ConfidenceBandHigh).HasColumnType("numeric(6,4)");

        ConfigureMoney(builder.ComplexProperty(v => v.Value));

        builder.HasOne<Holding>()
            .WithMany()
            .HasForeignKey(v => v.HoldingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<SpotPrice>()
            .WithMany()
            .HasForeignKey(v => v.DerivedFromSpotPriceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(v => new { v.HoldingId, v.ComputedAtUtc });
    }

    private static void ConfigureRefreshToken(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).UseIdentityByDefaultColumn();
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(t => t.DeviceLabel).HasMaxLength(200);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.FamilyId);
    }

    private static void ConfigureIdempotencyRecord(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).UseIdentityByDefaultColumn();
        builder.Property(r => r.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Endpoint).HasMaxLength(200).IsRequired();
        builder.Property(r => r.ResponseBody);

        builder.HasIndex(r => new { r.UserId, r.IdempotencyKey }).IsUnique();
    }

    private static void ConfigureReferenceImage(EntityTypeBuilder<ReferenceImage> builder)
    {
        builder.ToTable("reference_images");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).UseIdentityByDefaultColumn();
        builder.Property(i => i.CoinTypeId).HasConversion(Conversions.CoinTypeId);
        builder.Property(i => i.Side).HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.PerceptualHash).HasConversion(Conversions.PerceptualHash);
        builder.Property(i => i.StorageKey).HasMaxLength(300).IsRequired();

        // pgvector column + HNSW cosine index. The offline provider's
        // deterministic vectors are plumbing for future real embeddings
        // (documented as non-semantic in Identification/EmbeddingService).
        builder.Property(i => i.Embedding).HasColumnType("vector(768)");
        builder.HasIndex(i => i.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
        builder.HasIndex(i => i.CoinTypeId);

        builder.HasOne<CoinType>()
            .WithMany()
            .HasForeignKey(i => i.CoinTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureSeriesDemandTier(EntityTypeBuilder<SeriesDemandTierRow> builder)
    {
        builder.ToTable("series_demand_tiers");
        builder.HasKey(t => t.SeriesId);
        builder.Property(t => t.SeriesId).HasConversion(Conversions.SeriesId);
        builder.Property(t => t.Tier).HasConversion<string>().HasMaxLength(8);
    }
}
