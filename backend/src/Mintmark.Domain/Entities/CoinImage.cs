namespace Mintmark.Domain.Entities;

/// <summary>
/// A photo of one side of a <see cref="Holding"/>, stored in object storage
/// under <see cref="StorageKey"/> and fingerprinted with a perceptual hash
/// for dedupe. The embedding vector (pgvector, catalog matching) lives with
/// persistence in Infrastructure.
/// </summary>
public sealed class CoinImage
{
    /// <summary>Parameterless constructor for EF Core materialization only.</summary>
    private CoinImage()
    {
    }

    private CoinImage(string storageKey)
    {
        StorageKey = storageKey;
    }

    /// <summary>Gets the persistence-assigned identifier.</summary>
    public ImageId Id { get; private set; }

    /// <summary>Gets the holding the photo belongs to.</summary>
    public HoldingId HoldingId { get; private set; }

    /// <summary>Gets which side (or the slab) the photo shows.</summary>
    public CoinSide Side { get; private set; }

    /// <summary>Gets the object-storage key of the image bytes.</summary>
    public string StorageKey { get; private set; } = string.Empty;

    /// <summary>Gets the 64-bit perceptual hash used for dedupe.</summary>
    public ulong PerceptualHash { get; private set; }

    /// <summary>Gets when the photo was captured (UTC), if known.</summary>
    public DateTimeOffset? CapturedAtUtc { get; private set; }

    /// <summary>Gets the content type of the stored bytes, if known.</summary>
    public string? ContentType { get; private set; }

    /// <summary>Gets free-form notes.</summary>
    public string? Notes { get; private set; }

    /// <summary>Creates an image record, enforcing its invariants.</summary>
    /// <exception cref="ArgumentException">Thrown when the storage key is missing.</exception>
    public static CoinImage Create(
        HoldingId holdingId,
        CoinSide side,
        string storageKey,
        ulong perceptualHash,
        DateTimeOffset? capturedAtUtc = null,
        string? contentType = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("A storage key is required.", nameof(storageKey));
        }

        return new CoinImage(storageKey.Trim())
        {
            HoldingId = holdingId,
            Side = side,
            PerceptualHash = perceptualHash,
            CapturedAtUtc = capturedAtUtc?.ToUniversalTime(),
            ContentType = contentType,
            Notes = notes,
        };
    }
}
