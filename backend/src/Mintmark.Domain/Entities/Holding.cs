using Mintmark.Domain.ValueObjects;

namespace Mintmark.Domain.Entities;

/// <summary>
/// One owned item (or a lot of identical items). The purchase price per unit
/// is immutable: corrections append a <see cref="HoldingRevision"/>; the
/// effective values are read through the latest revision.
/// </summary>
public sealed class Holding
{
    private readonly List<HoldingRevision> _revisions = [];

    /// <summary>Parameterless constructor for EF Core materialization only.</summary>
    private Holding()
    {
    }

    private Holding(UserId userId, Money purchasePricePerUnit, DateTimeOffset purchasedAtUtc)
    {
        UserId = userId;
        PurchasePricePerUnit = purchasePricePerUnit;
        PurchasedAtUtc = purchasedAtUtc;
    }

    /// <summary>Gets the persistence-assigned identifier.</summary>
    public HoldingId Id { get; private set; }

    /// <summary>Gets the owning user.</summary>
    public UserId UserId { get; private set; }

    /// <summary>Gets the cataloged coin type; <c>null</c> for generic bars/rounds/scrap/jewelry.</summary>
    public CoinTypeId? CoinTypeId { get; private set; }

    /// <summary>Gets the physical form of the item.</summary>
    public ItemForm Form { get; private set; }

    /// <summary>Gets the original recorded quantity (corrections land in <see cref="Revisions"/>).</summary>
    public int Quantity { get; private set; }

    /// <summary>Gets the original purchase date (UTC).</summary>
    public DateTimeOffset PurchasedAtUtc { get; private set; }

    /// <summary>Gets the immutable original purchase price per unit.</summary>
    public Money PurchasePricePerUnit { get; private set; }

    /// <summary>Gets the dealer, if recorded (sensitive: see security doc).</summary>
    public string? Dealer { get; private set; }

    /// <summary>Gets the storage location, if recorded (sensitive: see security doc).</summary>
    public string? StorageLocation { get; private set; }

    /// <summary>Gets the serial number, if any.</summary>
    public string? SerialNumber { get; private set; }

    /// <summary>Gets the packaging state, if any.</summary>
    public string? PackagingState { get; private set; }

    /// <summary>Gets free-form notes.</summary>
    public string? Notes { get; private set; }

    /// <summary>Gets a value indicating whether the holding was soft-deleted.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Gets the append-only correction history for price/quantity.</summary>
    public IReadOnlyList<HoldingRevision> Revisions => _revisions;

    /// <summary>
    /// Gets the quantity in force: the latest revision's, or the original when
    /// no revisions exist.
    /// </summary>
    public int EffectiveQuantity => _revisions.Count > 0 ? _revisions[^1].Quantity : Quantity;

    /// <summary>
    /// Gets the purchase price per unit in force: the latest revision's, or
    /// the original when no revisions exist.
    /// </summary>
    public Money EffectivePurchasePricePerUnit =>
        _revisions.Count > 0 ? _revisions[^1].PurchasePricePerUnit : PurchasePricePerUnit;

    /// <summary>Creates a holding, enforcing its invariants.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown when quantity is below 1, the price is negative, or a Coin/Ingot
    /// is created without a <see cref="CoinTypeId"/>.
    /// </exception>
    public static Holding Create(
        UserId userId,
        ItemForm form,
        int quantity,
        DateTimeOffset purchasedAtUtc,
        Money purchasePricePerUnit,
        CoinTypeId? coinTypeId = null,
        string? dealer = null,
        string? storageLocation = null,
        string? serialNumber = null,
        string? packagingState = null,
        string? notes = null)
    {
        if (quantity < 1)
        {
            throw new ArgumentException($"Quantity must be at least 1; got {quantity}.", nameof(quantity));
        }

        if (purchasePricePerUnit.Amount < 0m)
        {
            throw new ArgumentException(
                $"Purchase price per unit cannot be negative; got {purchasePricePerUnit}.", nameof(purchasePricePerUnit));
        }

        if ((form == ItemForm.Coin || form == ItemForm.Ingot) && coinTypeId is null)
        {
            throw new ArgumentException(
                $"A holding of form {form} requires a CoinTypeId; generic cataloging is only allowed for Bar/Round/JunkSilver/Scrap/Jewelry.",
                nameof(coinTypeId));
        }

        return new Holding(userId, purchasePricePerUnit, purchasedAtUtc.ToUniversalTime())
        {
            CoinTypeId = coinTypeId,
            Form = form,
            Quantity = quantity,
            Dealer = dealer,
            StorageLocation = storageLocation,
            SerialNumber = serialNumber,
            PackagingState = packagingState,
            Notes = notes,
        };
    }

    /// <summary>
    /// Appends a correction revision. The original price/quantity are never
    /// mutated; the revision becomes the effective values.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the corrected quantity is below 1, the reason is missing,
    /// or the corrected price is in a different currency than the holding.
    /// </exception>
    public HoldingRevision AppendRevision(
        int correctedQuantity,
        Money correctedPurchasePricePerUnit,
        string reason,
        DateTimeOffset? revisedAtUtc = null)
    {
        if (correctedQuantity < 1)
        {
            throw new ArgumentException(
                $"Corrected quantity must be at least 1; got {correctedQuantity}.", nameof(correctedQuantity));
        }

        if (correctedPurchasePricePerUnit.Amount < 0m)
        {
            throw new ArgumentException(
                $"Corrected purchase price per unit cannot be negative; got {correctedPurchasePricePerUnit}.",
                nameof(correctedPurchasePricePerUnit));
        }

        if (correctedPurchasePricePerUnit.Currency != PurchasePricePerUnit.Currency)
        {
            throw new ArgumentException(
                $"Revisions must keep the holding currency {PurchasePricePerUnit.Currency}; got {correctedPurchasePricePerUnit.Currency}.",
                nameof(correctedPurchasePricePerUnit));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A revision reason is required.", nameof(reason));
        }

        var revision = new HoldingRevision(
            (ushort)(_revisions.Count + 1),
            correctedQuantity,
            correctedPurchasePricePerUnit,
            reason.Trim(),
            revisedAtUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow);
        _revisions.Add(revision);
        return revision;
    }

    /// <summary>Marks the holding as soft-deleted.</summary>
    public void SoftDelete() => IsDeleted = true;
}
