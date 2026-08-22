namespace Mintmark.Domain.Entities;

/// <summary>
/// A mint that strikes (or struck) coins. Reference data; seeded with the
/// mints listed in the master brief. A mint may carry several marks
/// (<c>W</c>, <c>S</c>, <c>P</c>, <c>D</c> for the US; <c>Mo</c> for Mexico City).
/// </summary>
public sealed class Mint
{
    private readonly List<string> _mintMarks = [];

    /// <summary>Parameterless constructor for EF Core materialization only.</summary>
    private Mint()
    {
    }

    private Mint(string name, string country, string countryCode)
    {
        Name = name;
        Country = country;
        CountryCode = countryCode;
    }

    /// <summary>Gets the persistence-assigned identifier.</summary>
    public MintId Id { get; private set; }

    /// <summary>Gets the display name of the mint.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the country the mint operates in.</summary>
    public string Country { get; private set; } = string.Empty;

    /// <summary>Gets the two-letter ISO country code.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>Gets the mint marks associated with this mint, normalized to uppercase.</summary>
    public IReadOnlyList<string> MintMarks => _mintMarks;

    /// <summary>Gets the year the mint was founded, if known.</summary>
    public int? FoundedYear { get; private set; }

    /// <summary>Gets a value indicating whether the mint is currently striking.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets free-form notes.</summary>
    public string? Notes { get; private set; }

    /// <summary>Gets the asset key of the mint logo, if any.</summary>
    public string? LogoAssetKey { get; private set; }

    /// <summary>Creates a mint, validating its invariants.</summary>
    /// <exception cref="ArgumentException">Thrown when name, country or country code is missing or malformed.</exception>
    public static Mint Create(
        string name,
        string country,
        string countryCode,
        IEnumerable<string>? mintMarks = null,
        int? foundedYear = null,
        bool isActive = true,
        string? notes = null,
        string? logoAssetKey = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Mint name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            throw new ArgumentException("Mint country is required.", nameof(country));
        }

        var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
        if (normalizedCountryCode.Length != 2 || normalizedCountryCode.Any(c => c is < 'A' or > 'Z'))
        {
            throw new ArgumentException(
                $"Country code must be exactly two uppercase letters; got '{countryCode}'.", nameof(countryCode));
        }

        var mint = new Mint(name.Trim(), country.Trim(), normalizedCountryCode)
        {
            FoundedYear = foundedYear,
            IsActive = isActive,
            Notes = notes,
            LogoAssetKey = logoAssetKey,
        };

        foreach (var mark in mintMarks ?? [])
        {
            mint.AddMintMark(mark);
        }

        return mint;
    }

    /// <summary>Adds a mint mark (idempotent, normalized to uppercase).</summary>
    /// <exception cref="ArgumentException">Thrown when the mark is empty.</exception>
    public void AddMintMark(string mark)
    {
        if (string.IsNullOrWhiteSpace(mark))
        {
            throw new ArgumentException("Mint mark cannot be empty.", nameof(mark));
        }

        var normalized = mark.Trim().ToUpperInvariant();
        if (!_mintMarks.Contains(normalized))
        {
            _mintMarks.Add(normalized);
        }
    }
}
