namespace Mintmark.Domain.ValueObjects;

/// <summary>Units of weight used by precious-metals specifications.</summary>
public enum WeightUnit
{
    /// <summary>Grams.</summary>
    Grams,

    /// <summary>Troy ounces.</summary>
    TroyOunces,
}

/// <summary>
/// A weight magnitude with its unit. All conversions funnel through the
/// single exact factor <see cref="GramsPerTroyOunce"/> so there is exactly
/// one place where unit math can go wrong — and it is unit-tested against
/// known values.
/// </summary>
public readonly record struct Weight
{
    /// <summary>The exact conversion factor: 1 troy ounce = 31.1034768 grams.</summary>
    public const decimal GramsPerTroyOunce = 31.1034768m;

    /// <summary>Initializes a weight. Negative magnitudes are rejected.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="magnitude"/> is negative.</exception>
    public Weight(decimal magnitude, WeightUnit unit)
    {
        if (magnitude < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(magnitude), magnitude, "Weight magnitude cannot be negative.");
        }

        Magnitude = magnitude;
        Unit = unit;
    }

    /// <summary>Gets the numeric magnitude in <see cref="Unit"/>.</summary>
    public decimal Magnitude { get; }

    /// <summary>Gets the unit of <see cref="Magnitude"/>.</summary>
    public WeightUnit Unit { get; }

    /// <summary>Creates a weight in grams.</summary>
    public static Weight Grams(decimal grams) => new(grams, WeightUnit.Grams);

    /// <summary>Creates a weight in troy ounces.</summary>
    public static Weight TroyOunces(decimal troyOunces) => new(troyOunces, WeightUnit.TroyOunces);

    /// <summary>Converts to grams using the exact factor.</summary>
    public decimal ToGrams() => Unit == WeightUnit.Grams ? Magnitude : Magnitude * GramsPerTroyOunce;

    /// <summary>Converts to troy ounces using the exact factor.</summary>
    public decimal ToTroyOunces() => Unit == WeightUnit.TroyOunces ? Magnitude : Magnitude / GramsPerTroyOunce;
}
