using Mintmark.Application.Ports;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Application.Tests.TestInfrastructure;

/// <summary>
/// The golden valuation fixture set: fictional-but-deterministic coins.
/// Fictional on purpose — golden tests freeze behavior, not any real mint's
/// catalog data.
/// </summary>
internal static class FixtureCoins
{
    public static readonly SeriesId EagleSeriesId = new(101);
    public static readonly SeriesId LibertadSeriesId = new(102);
    public static readonly SeriesId GoldEagleSeriesId = new(103);
    public static readonly MintId WestPointId = new(1);
    public static readonly MintId MexicoCityId = new(2);
    public static readonly UserId OwnerId = new(501);
    public static readonly HoldingId LibertadHoldingId = new(9001);

    public static readonly Currency Usd = new("USD");

    /// <summary>The fixed silver spot used by every golden test: $28.50/ozt.</summary>
    public const decimal SilverSpot = 28.50m;

    /// <summary>The fixed gold spot used by every golden test: $2,650.00/ozt.</summary>
    public const decimal GoldSpot = 2650.00m;

    public static Series SilverEagleSeries() =>
        Series.Create("Fictional Silver Eagle", WestPointId, MetalKind.Silver, startYear: 1986);

    public static Series LibertadSeries() =>
        Series.Create("Fictional Libertad", MexicoCityId, MetalKind.Silver, startYear: 1982);

    public static Series GoldEagleSeries() =>
        Series.Create("Fictional Gold Eagle", WestPointId, MetalKind.Gold, startYear: 1986);

    /// <summary>
    /// The "Eagle-style" leg of the divergence test: a common-date 1 oz
    /// silver bullion coin, mintage 14,000,000, BU, no flags. Near-pure melt.
    /// </summary>
    public static CoinType CommonEagleBullion() => CoinType.Create(
        EagleSeriesId,
        WestPointId,
        year: 2023,
        name: "Fictional Eagle-style 1 oz Silver Bullion 2023",
        finish: FinishPrimary.BullionUncirculated,
        fineness: 0.999m,
        grossWeightGrams: 31.103m,
        actualMetalWeightTroyOz: 1.000m,
        sourceUrl: "https://fixture.example.org/eagle-2023-1oz-bu",
        mintage: 14_000_000);

    /// <summary>
    /// The "Libertad-style" leg of the divergence test: a 2 oz reverse proof
    /// with HighRelief, mintage 1,800, graded MS70. Same metal, wildly
    /// different collectible.
    /// </summary>
    public static CoinType LibertadReverseProofHighRelief() => CoinType.Create(
        LibertadSeriesId,
        MexicoCityId,
        year: 2023,
        name: "Fictional Libertad-style 2 oz Reverse Proof High Relief 2023",
        finish: FinishPrimary.ReverseProof,
        fineness: 0.999m,
        grossWeightGrams: 62.207m,
        actualMetalWeightTroyOz: 2.000m,
        sourceUrl: "https://fixture.example.org/libertad-2023-2oz-rp-hr",
        finishAttributes: FinishAttribute.HighRelief,
        mintage: 1_800);

    /// <summary>A common-ish 1 oz gold bullion coin: mintage 80,000, medium demand.</summary>
    public static CoinType GoldEagleBullion() => CoinType.Create(
        GoldEagleSeriesId,
        WestPointId,
        year: 2023,
        name: "Fictional Gold Eagle-style 1 oz 2023",
        finish: FinishPrimary.BullionUncirculated,
        fineness: 0.9167m,
        grossWeightGrams: 33.931m,
        actualMetalWeightTroyOz: 1.000m,
        sourceUrl: "https://fixture.example.org/gold-eagle-2023-1oz-bu",
        mintage: 80_000);

    /// <summary>A pre-1936 issue for the age-band factor.</summary>
    public static CoinType MorganStyle1921() => CoinType.Create(
        EagleSeriesId,
        new MintId(3),
        year: 1921,
        name: "Fictional Morgan-style Dollar 1921",
        finish: FinishPrimary.BusinessStrike,
        fineness: 0.900m,
        grossWeightGrams: 26.73m,
        actualMetalWeightTroyOz: 0.7734m,
        sourceUrl: "https://fixture.example.org/morgan-1921",
        mintage: 20_000_000);

    public static Grading Ms70Pcgs() =>
        Grading.Create(LibertadHoldingId, GradingService.PCGS, numericGrade: 70);

    /// <summary>A single-unit holding of the given coin type, purchased at the given price.</summary>
    public static Holding SingleUnitHolding(CoinTypeId coinTypeId, decimal purchasePricePerUnit = 40m) =>
        Holding.Create(
            OwnerId,
            ItemForm.Coin,
            quantity: 1,
            purchasedAtUtc: new DateTimeOffset(2024, 3, 15, 12, 0, 0, TimeSpan.Zero),
            purchasePricePerUnit: new Money(purchasePricePerUnit, Usd),
            coinTypeId: coinTypeId);
}

/// <summary>A price provider frozen at the golden spots.</summary>
internal sealed class FixedSpotPriceProvider : IMetalPriceProvider
{
    public static readonly DateTimeOffset SourceTimestampUtc = new(2026, 8, 27, 16, 0, 0, TimeSpan.Zero);
    public const string ProviderName = "fixture-metals";

    public Task<ProviderQuote> GetCurrentAsync(
        MetalKind metal,
        Currency currency,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(metal switch
        {
            MetalKind.Silver => Quote(FixtureCoins.SilverSpot, 28.40m, 28.60m, currency),
            MetalKind.Gold => Quote(FixtureCoins.GoldSpot, 2648.00m, 2652.00m, currency),
            _ => throw new NotSupportedException($"No fixed spot for {metal} in the fixture."),
        });

    public Task<IReadOnlyList<DailySpotQuote>> GetDailyHistoryAsync(
        MetalKind metal,
        Currency currency,
        DateRange range,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DailySpotQuote>>([]);

    private static ProviderQuote Quote(decimal mid, decimal bid, decimal ask, Currency currency) => new(
        new Money(mid, currency),
        new Money(bid, currency),
        new Money(ask, currency),
        ProviderName,
        SourceTimestampUtc);
}
