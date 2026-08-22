using Mintmark.Domain.ValueObjects;

namespace Mintmark.Domain.Services;

/// <summary>The cost inputs of one holding for portfolio math.</summary>
/// <param name="PricePerUnit">Effective purchase price per unit.</param>
/// <param name="Quantity">Effective quantity.</param>
public readonly record struct HoldingCost(Money PricePerUnit, int Quantity);

/// <summary>One metal's slice of a portfolio value, for allocation weighting.</summary>
/// <param name="Metal">The metal.</param>
/// <param name="Value">The total value attributed to the metal.</param>
public readonly record struct MetalAllocation(MetalKind Metal, Money Value);

/// <summary>
/// Portfolio arithmetic — cost basis vs current value, unrealized gain/loss,
/// allocation weights — built entirely on <see cref="Money"/> so currency
/// mistakes throw instead of silently summing.
/// </summary>
public static class PortfolioMath
{
    /// <summary>Sums the cost basis: Σ (price per unit x quantity). Same currency required.</summary>
    /// <exception cref="InvalidOperationException">Thrown when holdings use mixed currencies.</exception>
    public static Money CostBasis(IReadOnlyList<HoldingCost> holdings, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(holdings);

        var total = Money.Zero(currency);
        foreach (var holding in holdings)
        {
            total += holding.PricePerUnit * holding.Quantity;
        }

        return total;
    }

    /// <summary>Sums current values. Same currency required.</summary>
    /// <exception cref="InvalidOperationException">Thrown when values use mixed currencies.</exception>
    public static Money TotalValue(IReadOnlyList<Money> values, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(values);

        var total = Money.Zero(currency);
        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }

    /// <summary>Computes the unrealized gain/loss as money (current − cost).</summary>
    public static Money UnrealizedGain(Money currentValue, Money costBasis) => currentValue - costBasis;

    /// <summary>
    /// Computes the unrealized gain/loss percentage:
    /// (current − cost) / cost x 100. A result of <c>12.34m</c> means +12.34%.
    /// </summary>
    /// <exception cref="DivideByZeroException">Thrown when the cost basis is zero.</exception>
    public static decimal UnrealizedGainPercent(Money currentValue, Money costBasis)
    {
        var gain = UnrealizedGain(currentValue, costBasis);
        return gain / costBasis * 100m;
    }

    /// <summary>
    /// Computes allocation weights by metal, as fractions of the total (0..1).
    /// The final metal's weight is adjusted so the weights sum to exactly 1
    /// (100%) despite decimal division rounding.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the input is empty or the total value is zero.</exception>
    /// <exception cref="InvalidOperationException">Thrown when values use mixed currencies.</exception>
    public static IReadOnlyDictionary<MetalKind, decimal> AllocateByMetal(IReadOnlyList<MetalAllocation> allocations)
    {
        ArgumentNullException.ThrowIfNull(allocations);

        if (allocations.Count == 0)
        {
            throw new ArgumentException("At least one allocation is required.", nameof(allocations));
        }

        // Sum per metal first (Money enforces a single currency).
        var byMetal = new Dictionary<MetalKind, Money>();
        foreach (var allocation in allocations)
        {
            if (byMetal.TryGetValue(allocation.Metal, out var existing))
            {
                byMetal[allocation.Metal] = existing + allocation.Value;
            }
            else
            {
                byMetal[allocation.Metal] = allocation.Value;
            }
        }

        var total = TotalValue([.. byMetal.Values], byMetal.First().Value.Currency);
        if (total.IsZero)
        {
            throw new ArgumentException("Cannot allocate a zero-total portfolio.", nameof(allocations));
        }

        var weights = new Dictionary<MetalKind, decimal>(byMetal.Count);
        var consumed = 0m;
        foreach (var (metal, value) in byMetal)
        {
            weights[metal] = value / total;
            consumed += weights[metal];
        }

        // Absorb decimal division rounding into the last metal so the weights
        // sum to exactly 1.
        var lastMetal = byMetal.Keys.Last();
        weights[lastMetal] = 1m - (consumed - weights[lastMetal]);
        return weights;
    }
}
