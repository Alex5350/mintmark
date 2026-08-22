namespace Mintmark.Domain.Entities;

/// <summary>
/// A coin series: one mint and one metal (American Silver Eagle, Libertad,
/// Maple Leaf, ...). Carries design metadata and the date range the series ran.
/// </summary>
public sealed class Series
{
    /// <summary>Parameterless constructor for EF Core materialization only.</summary>
    private Series()
    {
    }

    private Series(string name)
    {
        Name = name;
    }

    /// <summary>Gets the persistence-assigned identifier.</summary>
    public SeriesId Id { get; private set; }

    /// <summary>Gets the display name of the series.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the primary issuing mint.</summary>
    public MintId MintId { get; private set; }

    /// <summary>Gets the metal of the series.</summary>
    public MetalKind Metal { get; private set; }

    /// <summary>Gets the first year of issue, if known.</summary>
    public int? StartYear { get; private set; }

    /// <summary>Gets the last year of issue, if known (<c>null</c> while ongoing).</summary>
    public int? EndYear { get; private set; }

    /// <summary>Gets design notes.</summary>
    public string? Notes { get; private set; }

    /// <summary>Creates a series, validating its invariants.</summary>
    /// <exception cref="ArgumentException">Thrown when the name is missing or the year range is inverted.</exception>
    public static Series Create(
        string name,
        MintId mintId,
        MetalKind metal,
        int? startYear = null,
        int? endYear = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Series name is required.", nameof(name));
        }

        if (startYear.HasValue && endYear.HasValue && startYear.Value > endYear.Value)
        {
            throw new ArgumentException(
                $"Series start year {startYear} is after end year {endYear}.");
        }

        return new Series(name.Trim())
        {
            MintId = mintId,
            Metal = metal,
            StartYear = startYear,
            EndYear = endYear,
            Notes = notes,
        };
    }
}
