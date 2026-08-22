using FluentAssertions;
using Mintmark.Application.Tests.TestInfrastructure;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Domain.Services;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Application.Tests;

/// <summary>
/// The premium table is data, not code (ADR 0007): overriding weights changes
/// outputs without touching logic, and tier boundaries are exact.
/// </summary>
public class PremiumFactorTableTests
{
    private static readonly Currency Usd = new("USD");

    private static CoinType BullionWithMintage(long? mintage) => CoinType.Create(
        FixtureCoins.EagleSeriesId,
        FixtureCoins.WestPointId,
        year: 2023,
        name: "Fixture bullion",
        finish: FinishPrimary.BullionUncirculated,
        fineness: 0.999m,
        grossWeightGrams: 31.103m,
        actualMetalWeightTroyOz: 1.000m,
        sourceUrl: "https://fixture.example.org/tier-probe",
        mintage: mintage);

    [Fact]
    public void OverridingWeights_ChangesOutputs_DataNotCode()
    {
        // Default table: 3.0 x 1.8 x 1.15 x 1.6 x 1.4 = 13.9104.
        var defaultEstimate = new PremiumCalculator().Estimate(
            FixtureCoins.LibertadReverseProofHighRelief(),
            FixtureCoins.Ms70Pcgs(),
            SeriesDemandTier.High);
        defaultEstimate.Multiplier.Should().Be(13.9104m);

        // Override two weights as configuration/reference data:
        // ReverseProof 2.5, HighRelief 1.2 -> 3.0 x 2.5 x 1.2 x 1.6 x 1.4 = 20.16.
        var tunedTable = PremiumFactorTable.Default with
        {
            FinishReverseProof = 2.5m,
            FlagHighRelief = 1.2m,
        };
        var tunedEstimate = new PremiumCalculator(tunedTable).Estimate(
            FixtureCoins.LibertadReverseProofHighRelief(),
            FixtureCoins.Ms70Pcgs(),
            SeriesDemandTier.High);

        tunedEstimate.Multiplier.Should().Be(20.16m);

        var melt = new Money(57.00m, Usd);
        defaultEstimate.ApplyTo(melt).Collectible.Amount.Should().Be(792.8928m);
        tunedEstimate.ApplyTo(melt).Collectible.Amount.Should().Be(1149.12m); // 57.00 x 20.16

        // The breakdown reflects the overridden data.
        tunedEstimate.Factors.Single(f => f.FactorName == "FinishPrimary").Multiplier.Should().Be(2.5m);
        tunedEstimate.Factors.Single(f => f.FactorName == "FinishAttributeHighRelief").Multiplier.Should().Be(1.2m);
    }

    [Fact]
    public void OverridingConfidenceConstants_WidensTheBand()
    {
        var table = PremiumFactorTable.Default with
        {
            ConfidenceBaseHalfWidth = 0.10m,
            ConfidencePerAdditionalFactor = 0.02m,
        };
        var estimate = new PremiumCalculator(table).Estimate(
            FixtureCoins.LibertadReverseProofHighRelief(),
            FixtureCoins.Ms70Pcgs(),
            SeriesDemandTier.High);

        // 5 applied factors, 2 beyond the first three: 0.10 + 2 x 0.02 = 0.14.
        estimate.ConfidenceHalfWidth.Should().Be(0.14m);
        estimate.BandLowFraction.Should().Be(0.86m);
        estimate.BandHighFraction.Should().Be(1.14m);
    }

    [Theory]
    [InlineData(5000L, 3.0)]
    [InlineData(5001L, 2.0)]
    [InlineData(25000L, 2.0)]
    [InlineData(25001L, 1.5)]
    [InlineData(100000L, 1.5)]
    [InlineData(100001L, 1.15)]
    [InlineData(1000000L, 1.15)]
    [InlineData(1000001L, 1.0)]
    [InlineData(40000000L, 1.0)]
    public void MintageTier_Boundaries_AreExact(long mintage, decimal expectedTier)
    {
        // Everything else neutral (BU, raw, Low demand, modern), so the total
        // multiplier equals the mintage tier.
        var estimate = new PremiumCalculator().Estimate(
            BullionWithMintage(mintage),
            grading: null,
            SeriesDemandTier.Low);
        estimate.Multiplier.Should().Be(expectedTier);
        estimate.Factors.Single(f => f.FactorName == "MintageTier").Multiplier.Should().Be(expectedTier);
    }

    [Fact]
    public void MintageUnknown_IsNeutral()
    {
        var estimate = new PremiumCalculator().Estimate(
            BullionWithMintage(null),
            grading: null,
            SeriesDemandTier.Low);
        estimate.Factors.Single(f => f.FactorName == "MintageTier").Multiplier.Should().Be(1.0m);
        estimate.Factors.Single(f => f.FactorName == "MintageTier").Rationale.Should().Contain("unknown");
    }

    [Fact]
    public void Age_Pre1936_AppliesTheAgeFactor()
    {
        var estimate = new PremiumCalculator().Estimate(
            FixtureCoins.MorganStyle1921(),
            grading: null,
            SeriesDemandTier.Low);

        var age = estimate.Factors.Single(f => f.FactorName == "Age");
        age.Multiplier.Should().Be(1.25m);
        age.Rationale.Should().Contain("1921").And.Contain("1936");

        // Mintage 20M -> 1.0, BU -> 1.0, raw -> 1.0, Low -> 1.0, age -> 1.25.
        estimate.Multiplier.Should().Be(1.25m);
    }

    [Fact]
    public void CameoDesignation_AddsOnTopOfTheGradeFactor()
    {
        var gradedCameo = Grading.Create(
            FixtureCoins.LibertadHoldingId,
            GradingService.NGC,
            numericGrade: 70,
            designations: GradingDesignation.UltraCameo);

        var estimate = new PremiumCalculator().Estimate(
            FixtureCoins.LibertadReverseProofHighRelief(),
            gradedCameo,
            SeriesDemandTier.High);

        // 3.0 x 1.8 x 1.15 x 1.6 x 1.1 (cameo) x 1.4 = 15.30144.
        estimate.Multiplier.Should().Be(15.30144m);
        estimate.Factors.Single(f => f.FactorName == "DesignationCameo").Multiplier.Should().Be(1.1m);
    }

    [Fact]
    public void FinishFlagCombinations_MultiplyIndependently()
    {
        // Antiqued + Colorized on a matte proof: 1.5 x 1.05 x 1.1, all else neutral.
        var coin = CoinType.Create(
            FixtureCoins.EagleSeriesId,
            FixtureCoins.WestPointId,
            year: 2023,
            name: "Fixture fancy",
            finish: FinishPrimary.MatteProof,
            fineness: 0.999m,
            grossWeightGrams: 31.103m,
            actualMetalWeightTroyOz: 1.000m,
            sourceUrl: "https://fixture.example.org/fancy",
            finishAttributes: FinishAttribute.Antiqued | FinishAttribute.Colorized,
            mintage: 4000000L);

        var estimate = new PremiumCalculator().Estimate(coin, grading: null, SeriesDemandTier.Low);
        estimate.Multiplier.Should().Be(1.5m * 1.05m * 1.1m);
    }
}
