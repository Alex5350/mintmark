using Mintmark.Domain.ValueObjects;

namespace Mintmark.Domain.Entities;

/// <summary>
/// An append-only correction record for a <see cref="Holding"/>'s price
/// and/or quantity. Revisions are numbered sequentially per holding.
/// </summary>
public sealed class HoldingRevision
{
    /// <summary>Parameterless constructor for EF Core materialization only.</summary>
    private HoldingRevision()
    {
    }

    internal HoldingRevision(
        ushort revisionNumber,
        int quantity,
        Money purchasePricePerUnit,
        string reason,
        DateTimeOffset revisedAtUtc)
    {
        RevisionNumber = revisionNumber;
        Quantity = quantity;
        PurchasePricePerUnit = purchasePricePerUnit;
        Reason = reason;
        RevisedAtUtc = revisedAtUtc;
    }

    /// <summary>Gets the 1-based sequence number of this revision within its holding.</summary>
    public ushort RevisionNumber { get; private set; }

    /// <summary>Gets the corrected quantity.</summary>
    public int Quantity { get; private set; }

    /// <summary>Gets the corrected purchase price per unit.</summary>
    public Money PurchasePricePerUnit { get; private set; }

    /// <summary>Gets the reason for the correction.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Gets when the correction was recorded (UTC).</summary>
    public DateTimeOffset RevisedAtUtc { get; private set; }
}
