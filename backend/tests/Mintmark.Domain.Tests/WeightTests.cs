using Mintmark.Domain.ValueObjects;

namespace Mintmark.Domain.Tests;

public class WeightTests
{
    [Fact]
    public void OneTroyOunce_InGrams_IsExactFactor()
    {
        var grams = Weight.TroyOunces(1m).ToGrams();
        Assert.Equal(Weight.GramsPerTroyOunce, grams);
        Assert.Equal(31.1034768m, grams);
    }

    [Fact]
    public void GramsToTroyOunces_KnownValue()
    {
        // 31.1034768 g is exactly one troy ounce by the definition baked into the type.
        var ounces = Weight.Grams(31.1034768m).ToTroyOunces();
        Assert.Equal(1.0000m, ounces);
    }

    [Fact]
    public void Quarter_ActualSilverWeight_MathViaDecimal()
    {
        // A pre-1965 US quarter: 6.25 g gross at 90% silver.
        // ASW = 6.25 x 0.900 = 5.625 g of silver; in troy ounces that is the
        // well-known ~0.18084 ozt figure. All math is decimal.
        const decimal grossGrams = 6.25m;
        const decimal fineness = 0.900m;

        var actualSilverWeight = Weight.Grams(grossGrams * fineness);

        Assert.Equal(5.625m, actualSilverWeight.ToGrams());

        var ounces = actualSilverWeight.ToTroyOunces();
        // 5.625 / 31.1034768 = 0.18084794945...
        Assert.Equal(0.18084795m, ounces, precision: 8);

        // Round-trip: converting back to grams recovers the silver weight.
        var roundTripped = Weight.TroyOunces(ounces).ToGrams();
        Assert.Equal(5.625m, roundTripped, precision: 9);
    }

    [Fact]
    public void Units_RoundTripThroughBothDirections()
    {
        var original = Weight.TroyOunces(2m);
        var roundTripped = Weight.Grams(original.ToGrams());
        Assert.Equal(2m, roundTripped.ToTroyOunces(), precision: 12);
    }

    [Fact]
    public void Constructor_RejectsNegativeMagnitude()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Weight.Grams(-0.1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Weight.TroyOunces(-1m));
    }

    [Fact]
    public void StaticFactories_SetUnit()
    {
        Assert.Equal(WeightUnit.Grams, Weight.Grams(1m).Unit);
        Assert.Equal(WeightUnit.TroyOunces, Weight.TroyOunces(1m).Unit);
    }
}
