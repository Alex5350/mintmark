using System.Globalization;

namespace Mintmark.Domain;

// Strongly-typed identifiers: argument transposition becomes a compile error
// (see docs/domain-model.md). Backing type is `long` (snowflake/sequence
// friendly) for all v1 ids; the pattern is deliberately repeated per type so
// the Domain assembly stays dependency-free and each id remains an EF-friendly
// value converter target in Infrastructure.

/// <summary>Identifier of a <see cref="Entities.Holding"/>.</summary>
public readonly record struct HoldingId
{
    /// <summary>Initializes the identifier.</summary>
    public HoldingId(long value) => Value = value;

    /// <summary>Gets the raw identifier value.</summary>
    public long Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Identifier of a <see cref="Entities.CoinType"/> catalog row.</summary>
public readonly record struct CoinTypeId
{
    /// <summary>Initializes the identifier.</summary>
    public CoinTypeId(long value) => Value = value;

    /// <summary>Gets the raw identifier value.</summary>
    public long Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Identifier of a user account.</summary>
public readonly record struct UserId
{
    /// <summary>Initializes the identifier.</summary>
    public UserId(long value) => Value = value;

    /// <summary>Gets the raw identifier value.</summary>
    public long Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Identifier of a <see cref="Entities.Series"/>.</summary>
public readonly record struct SeriesId
{
    /// <summary>Initializes the identifier.</summary>
    public SeriesId(long value) => Value = value;

    /// <summary>Gets the raw identifier value.</summary>
    public long Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Identifier of a <see cref="Entities.Mint"/>.</summary>
public readonly record struct MintId
{
    /// <summary>Initializes the identifier.</summary>
    public MintId(long value) => Value = value;

    /// <summary>Gets the raw identifier value.</summary>
    public long Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Identifier of a <see cref="Entities.CoinImage"/>.</summary>
public readonly record struct ImageId
{
    /// <summary>Initializes the identifier.</summary>
    public ImageId(long value) => Value = value;

    /// <summary>Gets the raw identifier value.</summary>
    public long Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Identifier of an <see cref="Entities.IdentificationRun"/>.</summary>
public readonly record struct IdentificationRunId
{
    /// <summary>Initializes the identifier.</summary>
    public IdentificationRunId(long value) => Value = value;

    /// <summary>Gets the raw identifier value.</summary>
    public long Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Identifier of a <see cref="Entities.Valuation"/>.</summary>
public readonly record struct ValuationId
{
    /// <summary>Initializes the identifier.</summary>
    public ValuationId(long value) => Value = value;

    /// <summary>Gets the raw identifier value.</summary>
    public long Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Identifier of a <see cref="Entities.SpotPrice"/> row (FK target of valuations).</summary>
public readonly record struct SpotPriceId
{
    /// <summary>Initializes the identifier.</summary>
    public SpotPriceId(long value) => Value = value;

    /// <summary>Gets the raw identifier value.</summary>
    public long Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
