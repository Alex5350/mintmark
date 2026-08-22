using Mintmark.Domain.ValueObjects;

namespace Mintmark.Domain.Tests;

public class MoneyTests
{
    private static readonly Currency Usd = new("USD");
    private static readonly Currency Eur = new("EUR");

    [Fact]
    public void Add_SameCurrency_SumsAmounts()
    {
        var sum = new Money(28.50m, Usd) + new Money(1.25m, Usd);
        Assert.Equal(29.75m, sum.Amount);
        Assert.Equal(Usd, sum.Currency);
    }

    [Fact]
    public void Subtract_SameCurrency_SubtractsAmounts()
    {
        var difference = new Money(29.75m, Usd) - new Money(1.25m, Usd);
        Assert.Equal(28.50m, difference.Amount);
        Assert.Equal(Usd, difference.Currency);
    }

    [Fact]
    public void Add_CrossCurrency_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = new Money(28.50m, Usd) + new Money(10m, Eur);
        });
    }

    [Fact]
    public void Subtract_CrossCurrency_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = new Money(28.50m, Usd) - new Money(10m, Eur);
        });
    }

    [Fact]
    public void Multiply_ByDecimal_ScalesAmount()
    {
        var product = new Money(28.50m, Usd) * 2m;
        Assert.Equal(57.00m, product.Amount);
        Assert.Equal(Usd, product.Currency);
    }

    [Fact]
    public void Divide_MoneyByMoney_ReturnsScalarRatio()
    {
        var ratio = new Money(57.00m, Usd) / new Money(28.50m, Usd);
        Assert.Equal(2m, ratio);
    }

    [Fact]
    public void Zero_Factory_CreatesZeroAmountInCurrency()
    {
        var zero = Money.Zero(Usd);
        Assert.Equal(0m, zero.Amount);
        Assert.Equal(Usd, zero.Currency);
        Assert.True(zero.IsZero);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("usd")]
    [InlineData("USDD")]
    [InlineData("D12")]
    [InlineData("")]
    public void Currency_RejectsMalformedCodes(string code)
    {
        Assert.Throws<ArgumentException>(() => new Currency(code));
    }

    [Fact]
    public void Currency_AcceptsThreeUppercaseLetters()
    {
        var currency = new Currency("MXN");
        Assert.Equal("MXN", currency.Code);
        Assert.Equal("MXN", currency.ToString());
    }
}
