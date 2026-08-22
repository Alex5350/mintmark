using FluentAssertions;
using Mintmark.Domain;
using Mintmark.Domain.Services;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Application.Tests;

public class PortfolioMathTests
{
    private static readonly Currency Usd = new("USD");

    [Fact]
    public void CostBasis_SumsAcrossHoldings()
    {
        var cost = PortfolioMath.CostBasis(
        [
            new HoldingCost(new Money(30.50m, Usd), Quantity: 2),
            new HoldingCost(new Money(1325.00m, Usd), Quantity: 1),
        ],
        Usd);

        cost.Amount.Should().Be(1386.00m); // 61.00 + 1325.00
    }

    [Fact]
    public void TotalValue_MixedCurrency_Throws()
    {
        var act = () => PortfolioMath.TotalValue(
            [new Money(10m, Usd), new Money(9m, new Currency("EUR"))],
            Usd);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UnrealizedGainPercent_Computes()
    {
        var percent = PortfolioMath.UnrealizedGainPercent(
            currentValue: new Money(1200m, Usd),
            costBasis: new Money(1000m, Usd));
        percent.Should().Be(20m); // +20%

        var loss = PortfolioMath.UnrealizedGainPercent(
            currentValue: new Money(750m, Usd),
            costBasis: new Money(1000m, Usd));
        loss.Should().Be(-25m);
    }

    [Fact]
    public void Allocations_ByMetal_SumToExactly100Percent()
    {
        // Deliberately indivisible: 400 + 311 + 100 = 811.
        var allocations = new List<MetalAllocation>
        {
            new(MetalKind.Silver, new Money(400m, Usd)),
            new(MetalKind.Gold, new Money(311m, Usd)),
            new(MetalKind.Platinum, new Money(100m, Usd)),
        };

        var weights = PortfolioMath.AllocateByMetal(allocations);

        weights.Values.Sum().Should().Be(1m);
        weights[MetalKind.Silver].Should().BeApproximately(0.4932m, 0.0001m);
        weights[MetalKind.Gold].Should().BeApproximately(0.3835m, 0.0001m);
        weights[MetalKind.Platinum].Should().BeApproximately(0.1233m, 0.0001m);
    }

    [Fact]
    public void Allocations_RoundNumbers_AreExact()
    {
        var weights = PortfolioMath.AllocateByMetal(
        [
            new MetalAllocation(MetalKind.Silver, new Money(400m, Usd)),
            new MetalAllocation(MetalKind.Gold, new Money(600m, Usd)),
        ]);

        weights[MetalKind.Silver].Should().Be(0.4m);
        weights[MetalKind.Gold].Should().Be(0.6m);
        weights.Values.Sum().Should().Be(1m);
    }

    [Fact]
    public void Allocations_SingleMetal_IsFullWeight()
    {
        var weights = PortfolioMath.AllocateByMetal(
            [new MetalAllocation(MetalKind.Gold, new Money(12345.67m, Usd))]);

        weights.Should().ContainSingle();
        weights[MetalKind.Gold].Should().Be(1m);
    }

    [Fact]
    public void Allocations_SameMetalEntries_AreAggregatedFirst()
    {
        var weights = PortfolioMath.AllocateByMetal(
        [
            new MetalAllocation(MetalKind.Silver, new Money(100m, Usd)),
            new MetalAllocation(MetalKind.Silver, new Money(100m, Usd)),
            new MetalAllocation(MetalKind.Gold, new Money(100m, Usd)),
        ]);

        // Silver aggregates to 200 of the 300 total.
        weights[MetalKind.Silver].Should().BeApproximately(0.6667m, 0.0001m);
        weights[MetalKind.Gold].Should().BeApproximately(0.3333m, 0.0001m);
        weights.Values.Sum().Should().Be(1m);
    }

    [Fact]
    public void Allocations_ZeroTotal_Throws()
    {
        var act = () => PortfolioMath.AllocateByMetal(
            [new MetalAllocation(MetalKind.Silver, new Money(0m, Usd))]);
        act.Should().Throw<ArgumentException>();
    }
}
