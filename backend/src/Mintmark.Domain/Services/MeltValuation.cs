using Mintmark.Domain.ValueObjects;

namespace Mintmark.Domain.Services;

/// <summary>One metal's contribution to a multi-metal melt computation.</summary>
/// <param name="Metal">The precious metal.</param>
/// <param name="ActualMetalWeight">The actual metal weight of the portion.</param>
/// <param name="Quantity">How many items carry this portion.</param>
public readonly record struct MetalComponent(MetalKind Metal, Weight ActualMetalWeight, int Quantity);

/// <summary>Melt-value arithmetic. The only place melt is computed: ASW/AGW x quantity x spot.</summary>
public static class MeltValuation
{
    /// <summary>
    /// Computes melt value: actual metal weight (troy oz) x quantity x spot
    /// price per troy ounce, all in decimal <see cref="Money"/> arithmetic.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when quantity is below 1 or the spot price is not positive.
    /// </exception>
    public static Money Estimate(Weight actualMetalWeight, int quantity, Money spotPricePerTroyOunce)
    {
        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be at least 1.");
        }

        if (spotPricePerTroyOunce.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(spotPricePerTroyOunce), spotPricePerTroyOunce, "Spot price must be positive.");
        }

        var troyOunces = actualMetalWeight.ToTroyOunces() * quantity;
        return spotPricePerTroyOunce * troyOunces;
    }

    /// <summary>
    /// Computes melt value for a multi-metal item, summing the precious
    /// portions only: components whose metal has a spot price in
    /// <paramref name="spotPricesPerMetal"/>; anything unsourced contributes
    /// zero rather than an invented number.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no component is priced, or the spot currencies differ.
    /// </exception>
    public static Money Estimate(
        IReadOnlyList<MetalComponent> components,
        IReadOnlyDictionary<MetalKind, Money> spotPricesPerMetal)
    {
        Money? total = null;
        foreach (var component in components)
        {
            if (!spotPricesPerMetal.TryGetValue(component.Metal, out var spot))
            {
                continue;
            }

            var portion = Estimate(component.ActualMetalWeight, component.Quantity, spot);
            if (total is { } accumulated)
            {
                // Same-currency enforcement comes from Money itself.
                total = accumulated + portion;
            }
            else
            {
                total = portion;
            }
        }

        if (total is not { } result)
        {
            throw new InvalidOperationException(
                "No precious component of this item is priced; melt value cannot be computed.");
        }

        return result;
    }
}
