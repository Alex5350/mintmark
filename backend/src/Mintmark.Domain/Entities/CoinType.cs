using Mintmark.Domain.ValueObjects;

namespace Mintmark.Domain.Entities;

/// <summary>
/// The canonical catalog row: one per series x year x size x finish x
/// mint-mark combination. The actual metal weight (troy oz, ASW/AGW) is the
/// only number melt value ever uses. Hard rule from the domain model: every
/// specification row carries a source URL; disputed or unavailable figures
/// stay <c>null</c>.
/// </summary>
public sealed class CoinType
{
    /// <summary>Parameterless constructor for EF Core materialization only.</summary>
    private CoinType()
    {
    }

    private CoinType(string name, string sourceUrl)
    {
        Name = name;
        SourceUrl = sourceUrl;
    }

    /// <summary>Gets the persistence-assigned identifier.</summary>
    public CoinTypeId Id { get; private set; }

    /// <summary>Gets the series this row belongs to.</summary>
    public SeriesId SeriesId { get; private set; }

    /// <summary>Gets the mint that struck this issue.</summary>
    public MintId MintId { get; private set; }

    /// <summary>Gets the catalog display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the year of issue.</summary>
    public int Year { get; private set; }

    /// <summary>Gets the primary finish (ADR 0003).</summary>
    public FinishPrimary Finish { get; private set; }

    /// <summary>Gets the finish attribute flags (ADR 0003).</summary>
    public FinishAttribute FinishAttributes { get; private set; }

    /// <summary>Gets the fineness as a fraction of 1 (0 &lt; fineness &lt;= 1, e.g. 0.999).</summary>
    public decimal Fineness { get; private set; }

    /// <summary>Gets the gross weight in grams (must be positive).</summary>
    public decimal GrossWeightGrams { get; private set; }

    /// <summary>Gets the actual metal weight in troy ounces (ASW/AGW; non-negative).</summary>
    public decimal ActualMetalWeightTroyOz { get; private set; }

    /// <summary>Gets the actual metal weight as a <see cref="Weight"/> value object.</summary>
    public Weight ActualMetalWeight => Weight.TroyOunces(ActualMetalWeightTroyOz);

    /// <summary>Gets the gross weight as a <see cref="Weight"/> value object.</summary>
    public Weight GrossWeight => Weight.Grams(GrossWeightGrams);

    /// <summary>Gets the diameter in millimeters, if published.</summary>
    public decimal? DiameterMillimeters { get; private set; }

    /// <summary>Gets the thickness in millimeters, if published.</summary>
    public decimal? ThicknessMillimeters { get; private set; }

    /// <summary>Gets the edge type, if known.</summary>
    public EdgeType? Edge { get; private set; }

    /// <summary>Gets the mintage figure with its source; <c>null</c> when unavailable or disputed.</summary>
    public long? Mintage { get; private set; }

    /// <summary>Gets the required source URL for the specification row.</summary>
    public string SourceUrl { get; private set; } = string.Empty;

    /// <summary>Gets the Krause KM number, if any.</summary>
    public string? KmNumber { get; private set; }

    /// <summary>Gets the Red Book reference, if any.</summary>
    public string? RedBookReference { get; private set; }

    /// <summary>Gets the obverse reference image key, if any.</summary>
    public string? ObverseImageKey { get; private set; }

    /// <summary>Gets the reverse reference image key, if any.</summary>
    public string? ReverseImageKey { get; private set; }

    /// <summary>Creates a catalog row, enforcing the specification invariants.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the name or source URL is missing, or fineness is not in (0, 1], or weights are invalid.
    /// </exception>
    public static CoinType Create(
        SeriesId seriesId,
        MintId mintId,
        int year,
        string name,
        FinishPrimary finish,
        decimal fineness,
        decimal grossWeightGrams,
        decimal actualMetalWeightTroyOz,
        string sourceUrl,
        FinishAttribute finishAttributes = FinishAttribute.None,
        long? mintage = null,
        decimal? diameterMillimeters = null,
        decimal? thicknessMillimeters = null,
        EdgeType? edge = null,
        string? kmNumber = null,
        string? redBookReference = null,
        string? obverseImageKey = null,
        string? reverseImageKey = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("CoinType name is required.", nameof(name));
        }

        // Hard rule: every specification row carries a source URL. Disputed or
        // unavailable figures stay null — they do not get invented sources.
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            throw new ArgumentException(
                "CoinType requires a non-empty source URL for its specification figures.", nameof(sourceUrl));
        }

        if (fineness <= 0m || fineness > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fineness), fineness, "Fineness must be greater than 0 and at most 1 (e.g. 0.999).");
        }

        if (grossWeightGrams <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(grossWeightGrams), grossWeightGrams, "Gross weight must be greater than zero.");
        }

        if (actualMetalWeightTroyOz < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actualMetalWeightTroyOz), actualMetalWeightTroyOz, "Actual metal weight cannot be negative.");
        }

        if (year < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "Year of issue must be positive.");
        }

        return new CoinType(name.Trim(), sourceUrl.Trim())
        {
            SeriesId = seriesId,
            MintId = mintId,
            Year = year,
            Finish = finish,
            FinishAttributes = finishAttributes,
            Fineness = fineness,
            GrossWeightGrams = grossWeightGrams,
            ActualMetalWeightTroyOz = actualMetalWeightTroyOz,
            Mintage = mintage,
            DiameterMillimeters = diameterMillimeters,
            ThicknessMillimeters = thicknessMillimeters,
            Edge = edge,
            KmNumber = kmNumber,
            RedBookReference = redBookReference,
            ObverseImageKey = obverseImageKey,
            ReverseImageKey = reverseImageKey,
        };
    }
}
