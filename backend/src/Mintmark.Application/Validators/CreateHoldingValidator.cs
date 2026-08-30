using FluentValidation;
using Mintmark.Application.Dtos;
using Mintmark.Domain;

namespace Mintmark.Application.Validators;

/// <summary>
/// Validates holding creation requests: quantity, price, purchase date, and
/// the rule that cataloged forms (Coin/Ingot) require a CoinTypeId while
/// generic forms (Bar/Round/JunkSilver/Scrap/Jewelry) may omit it. String
/// lengths mirror the persistence maxima so oversized input is a 422, not a
/// database exception. Currency must be the tracker's base currency: spot
/// prices and valuations are single-currency today, so any other code would
/// silently never value (and the portfolio rollup would sum across units).
/// </summary>
public sealed class CreateHoldingValidator : AbstractValidator<CreateHoldingRequest>
{
    /// <summary>Initializes the validator with a clock (injectable for tests).</summary>
    public CreateHoldingValidator(TimeProvider? timeProvider = null, string? baseCurrency = null)
    {
        var clock = timeProvider ?? TimeProvider.System;

        RuleFor(x => x.ItemForm)
            .NotNull()
            .WithMessage("ItemForm is required.")
            .IsInEnum();

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PurchasePricePerUnit)
            .NotNull()
            .WithMessage("PurchasePricePerUnit is required.");

        RuleFor(x => x.PurchasePricePerUnit!.Amount)
            .GreaterThan(0m)
            .When(x => x.PurchasePricePerUnit is not null);

        RuleFor(x => x.PurchasePricePerUnit!.Currency)
            .NotEmpty()
            .Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be exactly three uppercase letters (e.g. USD).")
            .When(x => x.PurchasePricePerUnit is not null);

        if (baseCurrency is not null)
        {
            RuleFor(x => x.PurchasePricePerUnit!.Currency)
                .Must(currency => string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase))
                .WithMessage(
                    $"Currency must be the tracker's base currency ({baseCurrency}); multi-currency portfolios are not supported yet.")
                .When(x => x.PurchasePricePerUnit is not null);
        }

        RuleFor(x => x.PurchaseDate)
            .NotNull()
            .WithMessage("PurchaseDate is required.")
            .LessThanOrEqualTo(_ => clock.GetUtcNow())
            .WithMessage("PurchaseDate cannot be in the future.");

        RuleFor(x => x.CoinTypeId)
            .NotNull()
            .When(x => x.ItemForm is ItemForm.Coin or ItemForm.Ingot)
            .WithMessage(
                "CoinTypeId is required for Coin and Ingot items; only Bar/Round/JunkSilver/Scrap/Jewelry may be generic.");

        RuleFor(x => x.Dealer)
            .MaximumLength(200);

        RuleFor(x => x.StorageLocation)
            .MaximumLength(200);

        RuleFor(x => x.SerialNumber)
            .MaximumLength(100);

        RuleFor(x => x.Notes)
            .MaximumLength(2000);
    }
}
