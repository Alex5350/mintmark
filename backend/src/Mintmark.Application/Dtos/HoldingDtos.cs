using Mintmark.Domain;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Application.Dtos;

/// <summary>Money as it crosses the API edge: amount + three-letter currency code.</summary>
/// <param name="Amount">Decimal amount.</param>
/// <param name="Currency">Three-letter currency code (e.g. USD).</param>
public sealed record MoneyInput(decimal Amount, string Currency);

/// <summary>Request to create a holding (idempotent per idempotency key).</summary>
public sealed record CreateHoldingRequest
{
    /// <summary>Gets the caller-supplied idempotency key; retries with the same key return the original holding.</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Gets the cataloged coin type; required unless the form is Bar/Round/JunkSilver/Scrap/Jewelry.</summary>
    public long? CoinTypeId { get; init; }

    /// <summary>Gets the physical form of the item.</summary>
    public ItemForm? ItemForm { get; init; }

    /// <summary>Gets the item count (lot size); at least 1.</summary>
    public int Quantity { get; init; }

    /// <summary>Gets the purchase price per unit.</summary>
    public MoneyInput? PurchasePricePerUnit { get; init; }

    /// <summary>Gets the purchase date; must not be in the future.</summary>
    public DateTimeOffset? PurchaseDate { get; init; }

    /// <summary>Gets the dealer, if recorded.</summary>
    public string? Dealer { get; init; }

    /// <summary>Gets the storage location, if recorded (sensitive).</summary>
    public string? StorageLocation { get; init; }

    /// <summary>Gets the serial number, if any.</summary>
    public string? SerialNumber { get; init; }

    /// <summary>Gets free-form notes.</summary>
    public string? Notes { get; init; }
}

/// <summary>Response after creating (or idempotently re-submitting) a holding.</summary>
/// <param name="HoldingId">The holding identifier.</param>
/// <param name="Created">True when a new holding was created; false on an idempotent replay.</param>
public sealed record CreateHoldingResponse(HoldingId HoldingId, bool Created);

/// <summary>
/// Request to correct a holding. Corrections never mutate history: they
/// append a revision with a required reason.
/// </summary>
/// <param name="Quantity">Corrected quantity; omit to keep the current value.</param>
/// <param name="PurchasePricePerUnit">Corrected price per unit; omit to keep the current value.</param>
/// <param name="Reason">Why the correction is being made (required, audited).</param>
public sealed record UpdateHoldingRequest(int? Quantity, MoneyInput? PurchasePricePerUnit, string Reason);

/// <summary>Paged holding list request.</summary>
/// <param name="Cursor">Opaque pagination cursor; null starts from the beginning.</param>
/// <param name="Limit">Page size (1..100, default 50).</param>
public sealed record ListHoldingsRequest(string? Cursor, int Limit = 50);

/// <summary>One row of a holdings list.</summary>
public sealed record HoldingSummary
{
    /// <summary>Initializes the summary.</summary>
    public HoldingSummary(
        HoldingId id,
        string displayName,
        MetalKind? metal,
        ItemForm form,
        int effectiveQuantity,
        Money effectivePurchasePricePerUnit,
        Money? currentValue)
    {
        Id = id;
        DisplayName = displayName;
        Metal = metal;
        Form = form;
        EffectiveQuantity = effectiveQuantity;
        EffectivePurchasePricePerUnit = effectivePurchasePricePerUnit;
        CurrentValue = currentValue;
    }

    /// <summary>Gets the holding identifier.</summary>
    public HoldingId Id { get; }

    /// <summary>Gets a display label (catalog name, or a generic label).</summary>
    public string DisplayName { get; }

    /// <summary>Gets the metal, when cataloged.</summary>
    public MetalKind? Metal { get; }

    /// <summary>Gets the item form.</summary>
    public ItemForm Form { get; }

    /// <summary>Gets the quantity in force.</summary>
    public int EffectiveQuantity { get; }

    /// <summary>Gets the purchase price per unit in force.</summary>
    public Money EffectivePurchasePricePerUnit { get; }

    /// <summary>Gets the latest known total value, when computed.</summary>
    public Money? CurrentValue { get; }
}

/// <summary>A page of holdings.</summary>
/// <param name="Items">The holdings in this page.</param>
/// <param name="NextCursor">Cursor for the next page, or null at the end.</param>
public sealed record ListHoldingsResponse(IReadOnlyList<HoldingSummary> Items, string? NextCursor);

/// <summary>Holding detail, including current melt and collectible values.</summary>
public sealed record HoldingDetail
{
    /// <summary>Initializes the detail.</summary>
    public HoldingDetail(
        HoldingId id,
        CoinTypeId? coinTypeId,
        string displayName,
        ItemForm form,
        int originalQuantity,
        int effectiveQuantity,
        Money originalPurchasePricePerUnit,
        Money effectivePurchasePricePerUnit,
        int revisionCount,
        Money? currentMelt,
        Money? currentCollectible,
        DateTimeOffset purchasedAtUtc,
        bool isDeleted)
    {
        Id = id;
        CoinTypeId = coinTypeId;
        DisplayName = displayName;
        Form = form;
        OriginalQuantity = originalQuantity;
        EffectiveQuantity = effectiveQuantity;
        OriginalPurchasePricePerUnit = originalPurchasePricePerUnit;
        EffectivePurchasePricePerUnit = effectivePurchasePricePerUnit;
        RevisionCount = revisionCount;
        CurrentMelt = currentMelt;
        CurrentCollectible = currentCollectible;
        PurchasedAtUtc = purchasedAtUtc;
        IsDeleted = isDeleted;
    }

    /// <summary>Gets the holding identifier.</summary>
    public HoldingId Id { get; }

    /// <summary>Gets the cataloged coin type, when typed.</summary>
    public CoinTypeId? CoinTypeId { get; }

    /// <summary>Gets a display label.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the item form.</summary>
    public ItemForm Form { get; }

    /// <summary>Gets the originally recorded quantity.</summary>
    public int OriginalQuantity { get; }

    /// <summary>Gets the quantity in force.</summary>
    public int EffectiveQuantity { get; }

    /// <summary>Gets the immutable original purchase price per unit.</summary>
    public Money OriginalPurchasePricePerUnit { get; }

    /// <summary>Gets the purchase price per unit in force.</summary>
    public Money EffectivePurchasePricePerUnit { get; }

    /// <summary>Gets how many correction revisions exist.</summary>
    public int RevisionCount { get; }

    /// <summary>Gets the current melt value, when computable.</summary>
    public Money? CurrentMelt { get; }

    /// <summary>Gets the current collectible estimate, when computable.</summary>
    public Money? CurrentCollectible { get; }

    /// <summary>Gets the purchase date (UTC).</summary>
    public DateTimeOffset PurchasedAtUtc { get; }

    /// <summary>Gets a value indicating whether the holding was soft-deleted.</summary>
    public bool IsDeleted { get; }
}
