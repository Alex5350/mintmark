using FluentValidation;
using Mintmark.Application.Dtos;
using Mintmark.Domain;

namespace Mintmark.Application.Validators;

/// <summary>
/// Validates holding creation requests: quantity, price, purchase date, and
/// the rule that cataloged forms (Coin/Ingot) require a CoinTypeId while
/// generic forms (Bar/Round/JunkSilver/Scrap/Jewelry) may omit it.
/// </summary>
public sealed class CreateHoldingValidator : AbstractValidator<CreateHoldingRequest>
{
    /// <summary>Initializes the validator with a clock (injectable for tests).</summary>
    public CreateHoldingValidator(TimeProvider? timeProvider = null)
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
    }
}
