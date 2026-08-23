using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Infrastructure.Persistence;

/// <summary>
/// Reusable EF Core value conversions. Typed ids convert to/from <c>long</c>,
/// the 64-bit perceptual hash converts to/from <c>bigint</c> (PostgreSQL has
/// no unsigned bigint), and enum members convert to text (see ADR note in
/// <see cref="MintmarkDbContext"/>: text enums are simpler than Npgsql enum
/// types — readable rows, no type registration per connection).
/// </summary>
public static class Conversions
{
    /// <summary>Conversion for <see cref="HoldingId"/>.</summary>
    public static ValueConverter<HoldingId, long> HoldingId { get; } =
        new(v => v.Value, v => new HoldingId(v));

    /// <summary>Conversion for <see cref="CoinTypeId"/>.</summary>
    public static ValueConverter<CoinTypeId, long> CoinTypeId { get; } =
        new(v => v.Value, v => new CoinTypeId(v));

    /// <summary>Conversion for <see cref="UserId"/>.</summary>
    public static ValueConverter<UserId, long> UserId { get; } =
        new(v => v.Value, v => new UserId(v));

    /// <summary>Conversion for <see cref="SeriesId"/>.</summary>
    public static ValueConverter<SeriesId, long> SeriesId { get; } =
        new(v => v.Value, v => new SeriesId(v));

    /// <summary>Conversion for <see cref="MintId"/>.</summary>
    public static ValueConverter<MintId, long> MintId { get; } =
        new(v => v.Value, v => new MintId(v));

    /// <summary>Conversion for <see cref="ImageId"/>.</summary>
    public static ValueConverter<ImageId, long> ImageId { get; } =
        new(v => v.Value, v => new ImageId(v));

    /// <summary>Conversion for <see cref="IdentificationRunId"/>.</summary>
    public static ValueConverter<IdentificationRunId, long> IdentificationRunId { get; } =
        new(v => v.Value, v => new IdentificationRunId(v));

    /// <summary>Conversion for <see cref="ValuationId"/>.</summary>
    public static ValueConverter<ValuationId, long> ValuationId { get; } =
        new(v => v.Value, v => new ValuationId(v));

    /// <summary>Conversion for <see cref="SpotPriceId"/>.</summary>
    public static ValueConverter<SpotPriceId, long> SpotPriceId { get; } =
        new(v => v.Value, v => new SpotPriceId(v));

    /// <summary>Conversion for the 64-bit perceptual hash (<c>ulong</c> ↔ PostgreSQL <c>bigint</c>).</summary>
    public static ValueConverter<ulong, long> PerceptualHash { get; } =
        new(v => unchecked((long)v), v => unchecked((ulong)v));

    /// <summary>
    /// Conversion for <see cref="Currency"/> to a <c>char(3)</c> column.
    /// <see cref="Currency"/> re-validates on read, so corrupted codes fail loudly.
    /// </summary>
    public static ValueConverter<Currency, string> Currency { get; } =
        new(v => v.Code, v => new Currency(v));

    /// <summary>Gets a value comparer for JSON-converted immutable list properties.</summary>
    /// <typeparam name="T">The element type of the list.</typeparam>
    public static ValueComparer<IReadOnlyList<T>> ListComparer<T>()
        where T : IEquatable<T> =>
        new(
            (a, b) => (a ?? Array.Empty<T>()).SequenceEqual(b ?? Array.Empty<T>()),
            c => c == null ? 0 : c.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            c => c == null ? Array.Empty<T>() : c.ToArray());
}
