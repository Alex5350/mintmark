using Mintmark.Domain.Entities;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Domain.Tests;

public class CoinTypeTests
{
    private static readonly SeriesId SeriesId = new(1);
    private static readonly MintId MintId = new(2);

    private static CoinType Create(
        string sourceUrl = "https://catalog.example.org/issues/2023-1oz-bu",
        decimal fineness = 0.999m,
        decimal grossWeightGrams = 31.103m,
        decimal actualMetalWeightTroyOz = 1.000m,
        long? mintage = 14_000_000) =>
        CoinType.Create(
            SeriesId,
            MintId,
            year: 2023,
            name: "Test 1 oz Bullion",
            finish: FinishPrimary.BullionUncirculated,
            fineness: fineness,
            grossWeightGrams: grossWeightGrams,
            actualMetalWeightTroyOz: actualMetalWeightTroyOz,
            sourceUrl: sourceUrl,
            mintage: mintage);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingSourceUrl_Throws(string? sourceUrl)
    {
        var act = () => Create(sourceUrl: sourceUrl!);
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Create_FinenessAboveOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(fineness: 1.001m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void Create_FinenessNotPositive_Throws(double fineness)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(fineness: (decimal)fineness));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_GrossWeightNotPositive_Throws(double grossWeightGrams)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(grossWeightGrams: (decimal)grossWeightGrams));
    }

    [Fact]
    public void Create_NegativeActualMetalWeight_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(actualMetalWeightTroyOz: -0.01m));
    }

    [Fact]
    public void Create_Valid_PopulatesSpecification()
    {
        var coinType = Create();

        Assert.Equal(0.999m, coinType.Fineness);
        Assert.Equal(31.103m, coinType.GrossWeightGrams);
        Assert.Equal(1.000m, coinType.ActualMetalWeightTroyOz);
        Assert.Equal(14_000_000L, coinType.Mintage);
        Assert.Equal("https://catalog.example.org/issues/2023-1oz-bu", coinType.SourceUrl);
        Assert.Equal(FinishPrimary.BullionUncirculated, coinType.Finish);
        Assert.Equal(FinishAttribute.None, coinType.FinishAttributes);
    }

    [Fact]
    public void Create_TrimsSourceUrlAndName()
    {
        var coinType = Create(sourceUrl: "  https://catalog.example.org/x  ");
        Assert.Equal("https://catalog.example.org/x", coinType.SourceUrl);
    }

    [Fact]
    public void ActualMetalWeight_ExposesTroyOunceWeight()
    {
        var coinType = Create(actualMetalWeightTroyOz: 2m);
        Assert.Equal(WeightUnit.TroyOunces, coinType.ActualMetalWeight.Unit);
        Assert.Equal(2m, coinType.ActualMetalWeight.ToTroyOunces());
    }
}
