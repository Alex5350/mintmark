using Mintmark.Domain.Entities;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Domain.Tests;

public class HoldingTests
{
    private static readonly UserId UserId = new(7);
    private static readonly CoinTypeId CoinTypeId = new(11);
    private static readonly Currency Usd = new("USD");
    private static readonly Currency Eur = new("EUR");

    private static Holding Create(
        int quantity = 1,
        Money? pricePerUnit = null,
        ItemForm form = ItemForm.Coin,
        CoinTypeId? coinTypeId = null) =>
        Holding.Create(
            UserId,
            form,
            quantity,
            purchasedAtUtc: new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero),
            purchasePricePerUnit: pricePerUnit ?? new Money(30.00m, Usd),
            coinTypeId: coinTypeId ?? CoinTypeId);

    [Fact]
    public void Create_QuantityBelowOne_Throws()
    {
        Assert.Throws<ArgumentException>(() => Create(quantity: 0));
    }

    [Fact]
    public void Create_NegativePrice_Throws()
    {
        Assert.Throws<ArgumentException>(() => Create(pricePerUnit: new Money(-1m, Usd)));
    }

    [Fact]
    public void Create_CoinWithoutCoinTypeId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Holding.Create(
            UserId,
            ItemForm.Coin,
            quantity: 1,
            purchasedAtUtc: new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero),
            purchasePricePerUnit: new Money(30.00m, Usd),
            coinTypeId: null));
    }

    [Fact]
    public void Create_BarWithoutCoinTypeId_IsAllowed()
    {
        var bar = Holding.Create(
            UserId,
            ItemForm.Bar,
            quantity: 1,
            purchasedAtUtc: new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero),
            purchasePricePerUnit: new Money(30.00m, Usd),
            coinTypeId: null);
        Assert.Equal(ItemForm.Bar, bar.Form);
        Assert.Null(bar.CoinTypeId);
    }

    [Fact]
    public void AppendRevision_RecordsCorrection_OriginalStaysImmutable()
    {
        var holding = Create(quantity: 2, pricePerUnit: new Money(28.00m, Usd));

        var revision = holding.AppendRevision(
            correctedQuantity: 3,
            correctedPurchasePricePerUnit: new Money(31.50m, Usd),
            reason: "found the receipt; paid more than logged");

        // The original facts never change: corrections are new revisions.
        Assert.Equal(2, holding.Quantity);
        Assert.Equal(28.00m, holding.PurchasePricePerUnit.Amount);

        // The revision is appended and becomes the effective truth.
        Assert.Equal(1, revision.RevisionNumber);
        Assert.Single(holding.Revisions);
        Assert.Equal(3, holding.EffectiveQuantity);
        Assert.Equal(31.50m, holding.EffectivePurchasePricePerUnit.Amount);
    }

    [Fact]
    public void AppendRevision_SecondCorrection_NumbersSequentially()
    {
        var holding = Create();
        holding.AppendRevision(2, new Money(29m, Usd), "counted again");
        var second = holding.AppendRevision(4, new Money(30m, Usd), "bought two more");

        Assert.Equal(2, second.RevisionNumber);
        Assert.Equal(2, holding.Revisions.Count);
        Assert.Equal(4, holding.EffectiveQuantity);
    }

    [Fact]
    public void AppendRevision_CurrencyMismatch_Throws()
    {
        var holding = Create(pricePerUnit: new Money(28.00m, Usd));
        Assert.Throws<ArgumentException>(
            () => holding.AppendRevision(1, new Money(26.00m, Eur), "currency change"));
    }

    [Fact]
    public void AppendRevision_InvalidQuantity_Throws()
    {
        var holding = Create();
        Assert.Throws<ArgumentException>(() => holding.AppendRevision(0, new Money(29m, Usd), "typo"));
    }

    [Fact]
    public void AppendRevision_MissingReason_Throws()
    {
        var holding = Create();
        Assert.Throws<ArgumentException>(() => holding.AppendRevision(1, new Money(29m, Usd), "  "));
    }
}
