namespace Mintmark.Domain.ValueObjects;

/// <summary>
/// A three-letter currency code (for example <c>USD</c>). Accepted by
/// <see cref="Money"/> so that cross-currency arithmetic fails loudly.
/// </summary>
public readonly record struct Currency
{
    /// <summary>Initializes a currency code and validates its shape.</summary>
    /// <param name="code">Exactly three uppercase ASCII letters.</param>
    /// <exception cref="ArgumentException">Thrown when the code is not three uppercase letters.</exception>
    public Currency(string code)
    {
        if (!IsWellFormed(code))
        {
            throw new ArgumentException(
                $"Currency code must be exactly three uppercase letters (e.g. 'USD'); got '{code}'.", nameof(code));
        }

        Code = code;
    }

    /// <summary>Gets the three-letter code.</summary>
    public string Code { get; }

    /// <summary>Returns true when <paramref name="code"/> is three uppercase ASCII letters.</summary>
    public static bool IsWellFormed(string? code) =>
        code is not null
        && code.Length == 3
        && code[0] is >= 'A' and <= 'Z'
        && code[1] is >= 'A' and <= 'Z'
        && code[2] is >= 'A' and <= 'Z';

    /// <inheritdoc />
    public override string ToString() => Code;
}

/// <summary>
/// A decimal amount in a specific currency. All monetary arithmetic in
/// Mintmark goes through this type so that cross-currency mistakes throw at
/// the type level instead of corrupting valuations. Binary floating point
/// (<c>double</c>/<c>float</c>) is never used for money.
/// </summary>
public readonly record struct Money
{
    /// <summary>Initializes a monetary amount.</summary>
    public Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Initializes a monetary amount from a three-letter currency code.</summary>
    public Money(decimal amount, string currencyCode)
        : this(amount, new Currency(currencyCode))
    {
    }

    /// <summary>Gets the amount. May be negative (for example for losses).</summary>
    public decimal Amount { get; }

    /// <summary>Gets the currency of the amount.</summary>
    public Currency Currency { get; }

    /// <summary>Gets a value indicating whether the amount is exactly zero.</summary>
    public bool IsZero => Amount == 0m;

    /// <summary>Creates a zero amount in the given currency.</summary>
    public static Money Zero(Currency currency) => new(0m, currency);

    /// <summary>Creates a zero amount in the given currency.</summary>
    public static Money Zero(string currencyCode) => new(0m, new Currency(currencyCode));

    /// <summary>Adds two amounts of the same currency.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the currencies differ.</exception>
    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right, "add");
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    /// <summary>Subtracts two amounts of the same currency.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the currencies differ.</exception>
    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right, "subtract");
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    /// <summary>Negates the amount, keeping the currency.</summary>
    public static Money operator -(Money value) => new(-value.Amount, value.Currency);

    /// <summary>Scales an amount by a decimal factor.</summary>
    public static Money operator *(Money value, decimal factor) => new(value.Amount * factor, value.Currency);

    /// <summary>Scales an amount by a decimal factor.</summary>
    public static Money operator *(decimal factor, Money value) => value * factor;

    /// <summary>Divides an amount by a decimal divisor.</summary>
    /// <exception cref="DivideByZeroException">Thrown when <paramref name="divisor"/> is zero.</exception>
    public static Money operator /(Money value, decimal divisor) => new(value.Amount / divisor, value.Currency);

    /// <summary>
    /// Computes the ratio of two same-currency amounts as a plain decimal.
    /// Useful for percentages and allocation weights without leaking the
    /// amount out of its currency context by accident.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the currencies differ.</exception>
    /// <exception cref="DivideByZeroException">Thrown when <paramref name="divisor"/> is zero.</exception>
    public static decimal operator /(Money dividend, Money divisor)
    {
        EnsureSameCurrency(dividend, divisor, "divide");
        return dividend.Amount / divisor.Amount;
    }

    /// <summary>Rounds the amount to <paramref name="decimalPlaces"/> (banker's rounding), keeping the currency.</summary>
    public Money Round(int decimalPlaces) => new(Math.Round(Amount, decimalPlaces), Currency);

    private static void EnsureSameCurrency(Money left, Money right, string operation)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot {operation} {left.Currency} and {right.Currency}: cross-currency arithmetic is not supported. Convert first.");
        }
    }
}
