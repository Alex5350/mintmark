using Mintmark.Application.Tests.TestInfrastructure;
using Mintmark.Application.UseCases;
using Mintmark.Domain;
using Mintmark.Domain.Services;
using Mintmark.Domain.ValueObjects;
using FluentAssertions;

namespace Mintmark.Application.Tests;

/// <summary>
/// Golden valuation tests: fixed spots (Silver $28.50/ozt, Gold $2,650/ozt)
/// crossed with a fictional-but-deterministic fixture coin set produce the
/// exact expected decimals below. Any change to a premium factor is a
/// deliberate, reviewed diff that must update these numbers (ADR 0007).
/// </summary>
public class GoldenValuationTests
{
    private static readonly Currency Usd = new("USD");
    private static readonly PremiumCalculator Calculator = new();

    private static Money SilverSpot => new(FixtureCoins.SilverSpot, Usd);

    private static Money GoldSpot => new(FixtureCoins.GoldSpot, Usd);

    // ---------------------------------------------------------------- eagle

    [Fact]
    public void CommonEagle_MeltIsSpotTimesAmw_AndPremiumIsNeutral()
    {
        var coin = FixtureCoins.CommonEagleBullion();

        // 1.000 ozt x 1 x $28.50 = $28.50 exactly.
        var melt = MeltValuation.Estimate(coin.ActualMetalWeight, quantity: 1, SilverSpot);
        melt.Amount.Should().Be(28.50m);
        melt.Currency.Should().Be(Usd);

        // Every factor is neutral for a common-date raw BU coin.
        var premium = Calculator.Estimate(coin, grading: null, SeriesDemandTier.Low);
        premium.Multiplier.Should().Be(1.0m);
        premium.AppliedFactorCount.Should().Be(0);

        var estimate = premium.ApplyTo(melt);
        estimate.Collectible.Amount.Should().Be(28.50m);
        estimate.Premium.Amount.Should().Be(0.00m);

        // Zero applied factors: base confidence band only (+/-0.15).
        premium.ConfidenceHalfWidth.Should().Be(0.15m);
        premium.BandLowFraction.Should().Be(0.85m);
        premium.BandHighFraction.Should().Be(1.15m);
    }

    // ------------------------------------------------------------- libertad

    [Fact]
    public void LibertadStyle_MeltAndCollectible_AreExact()
    {
        var coin = FixtureCoins.LibertadReverseProofHighRelief();

        // 2.000 ozt x 1 x $28.50 = $57.00 exactly.
        var melt = MeltValuation.Estimate(coin.ActualMetalWeight, quantity: 1, SilverSpot);
        melt.Amount.Should().Be(57.00m);

        // Mintage 1,800 -> 3.0; ReverseProof -> 1.8; HighRelief -> 1.15;
        // MS70 -> 1.6; demand High -> 1.4; age 2023 -> 1.0.
        // 3.0 x 1.8 x 1.15 x 1.6 x 1.4 = 13.9104.
        var premium = Calculator.Estimate(coin, FixtureCoins.Ms70Pcgs(), SeriesDemandTier.High);
        premium.Multiplier.Should().Be(13.9104m);
        premium.AppliedFactorCount.Should().Be(5);

        var estimate = premium.ApplyTo(melt);
        estimate.Collectible.Amount.Should().Be(792.8928m); // 57.00 x 13.9104
        estimate.Premium.Amount.Should().Be(735.8928m);    // 792.8928 - 57.00

        // Five applied factors -> two beyond the first three -> +/- (0.15 + 0.10).
        premium.ConfidenceHalfWidth.Should().Be(0.25m);
        premium.BandLowFraction.Should().Be(0.75m);
        premium.BandHighFraction.Should().Be(1.25m);

        // Money bounds of the band.
        (estimate.Collectible * premium.BandLowFraction).Amount.Should().Be(594.6696m);
        (estimate.Collectible * premium.BandHighFraction).Amount.Should().Be(991.116m);
    }

    [Fact]
    public void LibertadStyle_ItemizedBreakdown_MatchesFactorByFactor()
    {
        var premium = Calculator.Estimate(
            FixtureCoins.LibertadReverseProofHighRelief(),
            FixtureCoins.Ms70Pcgs(),
            SeriesDemandTier.High);

        premium.Factors.Should().BeEquivalentTo(
        [
            new { FactorName = "MintageTier", Multiplier = 3.0m },
            new { FactorName = "FinishPrimary", Multiplier = 1.8m },
            new { FactorName = "FinishAttributeHighRelief", Multiplier = 1.15m },
            new { FactorName = "Grade", Multiplier = 1.6m },
            new { FactorName = "SeriesDemand", Multiplier = 1.4m },
            new { FactorName = "Age", Multiplier = 1.0m },
        ], options => options.WithStrictOrdering());

        // Every line carries a rationale: estimates are explainable.
        premium.Factors.Should().OnlyContain(f => !string.IsNullOrWhiteSpace(f.Rationale));
    }

    [Fact]
    public void LibertadStyle_GradeMs70VersusRaw_ChangesOnlyTheGradeFactor()
    {
        var coin = FixtureCoins.LibertadReverseProofHighRelief();

        var raw = Calculator.Estimate(coin, grading: null, SeriesDemandTier.High);
        var ms70 = Calculator.Estimate(coin, FixtureCoins.Ms70Pcgs(), SeriesDemandTier.High);

        // Raw: 3.0 x 1.8 x 1.15 x 1.4 = 8.694; MS70 additionally x 1.6 = 13.9104.
        raw.Multiplier.Should().Be(8.694m);
        ms70.Multiplier.Should().Be(13.9104m);

        var melt = new Money(57.00m, Usd);
        raw.ApplyTo(melt).Collectible.Amount.Should().Be(495.558m);  // 57.00 x 8.694
        ms70.ApplyTo(melt).Collectible.Amount.Should().Be(792.8928m);

        // Four applied factors for raw -> one beyond the first three -> +/-0.20.
        raw.ConfidenceHalfWidth.Should().Be(0.20m);
        raw.BandLowFraction.Should().Be(0.80m);
        raw.BandHighFraction.Should().Be(1.20m);
    }

    // ------------------------------------------------------------ divergence

    /// <summary>
    /// THE divergence test (ADR 0007): a low-mintage 2 oz reverse-proof
    /// Libertad-style coin vs a common-date Eagle-style coin. Near-identical
    /// melt (2x vs 1x AMW at the same spot), wildly different collectible —
    /// normalized by premium multiples, falling out of the factors alone with
    /// no special cases.
    /// </summary>
    [Fact]
    public void Divergence_LibertadVsEagle_PremiumMultiples()
    {
        var eagle = FixtureCoins.CommonEagleBullion();
        var libertad = FixtureCoins.LibertadReverseProofHighRelief();

        var eagleMelt = MeltValuation.Estimate(eagle.ActualMetalWeight, 1, SilverSpot);
        var libertadMelt = MeltValuation.Estimate(libertad.ActualMetalWeight, 1, SilverSpot);

        // Melts are close: exactly 2x apart (both pure silver at one spot).
        (libertadMelt / eagleMelt).Should().Be(2m);

        var eaglePremium = Calculator.Estimate(eagle, grading: null, SeriesDemandTier.Low);
        var libertadPremium = Calculator.Estimate(libertad, FixtureCoins.Ms70Pcgs(), SeriesDemandTier.High);

        var eagleMultiple = eaglePremium.ApplyTo(eagleMelt).Collectible / eagleMelt;
        var libertadMultiple = libertadPremium.ApplyTo(libertadMelt).Collectible / libertadMelt;

        // A common Eagle trades at melt: multiple ~= 1.0.
        eagleMultiple.Should().Be(1.0m);

        // The low-mintage reverse proof trades far above melt.
        libertadMultiple.Should().BeGreaterThanOrEqualTo(5m);

        // Exact frozen values.
        eagleMultiple.Should().Be(1.0m);
        libertadMultiple.Should().Be(13.9104m);

        // The ratio gap — what the melt-normalized comparison is really about.
        (libertadMultiple / eagleMultiple).Should().Be(13.9104m);
        (libertadMultiple - eagleMultiple).Should().Be(12.9104m);

        // In absolute money: melts 28.50 vs 57.00 (2x), collectibles
        // 28.50 vs 792.8928 (27.8x).
        var eagleCollectible = eaglePremium.ApplyTo(eagleMelt).Collectible;
        var libertadCollectible = libertadPremium.ApplyTo(libertadMelt).Collectible;
        eagleCollectible.Amount.Should().Be(28.50m);
        libertadCollectible.Amount.Should().Be(792.8928m);
        (libertadCollectible / eagleCollectible).Should().BeApproximately(27.8208m, 0.0001m);
    }

    // ----------------------------------------------------------------- gold

    [Fact]
    public void GoldEagle_MediumDemandLowMintage()
    {
        var coin = FixtureCoins.GoldEagleBullion();

        // 1.000 ozt x 1 x $2,650 = $2,650.00.
        var melt = MeltValuation.Estimate(coin.ActualMetalWeight, quantity: 1, GoldSpot);
        melt.Amount.Should().Be(2650.00m);

        // Mintage 80,000 (<=100k) -> 1.5; demand Medium -> 1.15; the rest neutral.
        // 1.5 x 1.15 = 1.725.
        var premium = Calculator.Estimate(coin, grading: null, SeriesDemandTier.Medium);
        premium.Multiplier.Should().Be(1.725m);

        var estimate = premium.ApplyTo(melt);
        estimate.Collectible.Amount.Should().Be(4571.25m);  // 2650 x 1.725
        estimate.Premium.Amount.Should().Be(1921.25m);      // 4571.25 - 2650

        premium.ConfidenceHalfWidth.Should().Be(0.15m);
    }

    // ---------------------------------------------------------- multi-metal

    [Fact]
    public void Melt_MultiMetal_SumsPreciousPortionsOnly()
    {
        // 2 ozt silver x1 @ 28.50 = 57.00
        // 0.5 ozt gold x2 @ 2,650 = 2,650.00
        // platinum portion unsourced -> contributes nothing
        var components = new List<MetalComponent>
        {
            new(MetalKind.Silver, Weight.TroyOunces(2.000m), 1),
            new(MetalKind.Gold, Weight.TroyOunces(0.500m), 2),
            new(MetalKind.Platinum, Weight.TroyOunces(1.000m), 1),
        };
        var spots = new Dictionary<MetalKind, Money>
        {
            [MetalKind.Silver] = SilverSpot,
            [MetalKind.Gold] = GoldSpot,
        };

        var melt = MeltValuation.Estimate(components, spots);
        melt.Amount.Should().Be(2707.00m); // 57.00 + 2650.00
        melt.Currency.Should().Be(Usd);
    }

    // ------------------------------------------------------ orchestration

    [Fact]
    public async Task ValuationService_ValuesLibertadStyle_AndStampsRulesV1()
    {
        var service = new ValuationService(new FixedSpotPriceProvider());
        var coin = FixtureCoins.LibertadReverseProofHighRelief();
        var holding = FixtureCoins.SingleUnitHolding(new CoinTypeId(2023));

        var dto = await service.ValueAsync(
            holding,
            coin,
            FixtureCoins.LibertadSeries(),
            SeriesDemandTier.High,
            FixtureCoins.Ms70Pcgs());

        dto.HoldingId.Should().Be(holding.Id);
        dto.Melt.Amount.Should().Be(57.00m);
        dto.Collectible.Amount.Should().Be(792.8928m);
        dto.Premium.Amount.Should().Be(735.8928m);
        dto.PremiumMultiplier.Should().Be(13.9104m);

        // The collectible method is stamped rules-v1.
        dto.Provenance.Method.Should().Be(ValuationService.CollectibleMethod);
        dto.Provenance.MethodVersion.Should().Be("rules-v1");
        dto.Provenance.Source.Should().Be(FixedSpotPriceProvider.ProviderName);
        dto.Provenance.SourceTimestampUtc.Should().Be(FixedSpotPriceProvider.SourceTimestampUtc);
        dto.Provenance.SpotPricePerTroyOunce.Amount.Should().Be(28.50m);

        // Band + itemized factors ride along.
        dto.ConfidenceBand.LowFraction.Should().Be(0.75m);
        dto.ConfidenceBand.HighFraction.Should().Be(1.25m);
        dto.ConfidenceBand.LowValue.Amount.Should().Be(594.6696m);
        dto.ConfidenceBand.HighValue.Amount.Should().Be(991.116m);
        dto.PremiumFactors.Should().HaveCount(6);
    }

    [Fact]
    public async Task ValuationService_UsesEffectiveQuantityAndHoldingCurrency()
    {
        var service = new ValuationService(new FixedSpotPriceProvider());
        var coin = FixtureCoins.CommonEagleBullion();
        var holding = FixtureCoins.SingleUnitHolding(new CoinTypeId(2024), purchasePricePerUnit: 33m);

        // A correction doubles the lot: melt follows the effective quantity.
        holding.AppendRevision(
            correctedQuantity: 2,
            correctedPurchasePricePerUnit: new Money(29.00m, Usd),
            reason: "found the second tube");

        var dto = await service.ValueAsync(
            holding,
            coin,
            FixtureCoins.SilverEagleSeries(),
            SeriesDemandTier.Low);

        // 2 x 1.000 ozt x $28.50 = $57.00.
        dto.Melt.Amount.Should().Be(57.00m);
        dto.Collectible.Amount.Should().Be(57.00m);
    }
}
