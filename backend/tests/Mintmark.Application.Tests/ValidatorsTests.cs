using FluentAssertions;
using FluentValidation;
using Mintmark.Application.Dtos;
using Mintmark.Application.Validators;
using Mintmark.Domain;

namespace Mintmark.Application.Tests;

public class ValidatorsTests
{
    // --------------------------------------------------- CreateHolding

    private static CreateHoldingRequest ValidCoinRequest() => new()
    {
        CoinTypeId = 42,
        ItemForm = ItemForm.Coin,
        Quantity = 1,
        PurchasePricePerUnit = new MoneyInput(33.50m, "USD"),
        PurchaseDate = DateTimeOffset.UtcNow.AddDays(-1),
    };

    [Fact]
    public void CreateHolding_ValidCoinRequest_Passes()
    {
        var result = new CreateHoldingValidator().Validate(ValidCoinRequest());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateHolding_Rejects_Currency_Other_Than_Base()
    {
        var request = ValidCoinRequest() with { PurchasePricePerUnit = new MoneyInput(33.50m, "EUR") };
        var result = new CreateHoldingValidator(baseCurrency: "USD").Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("base currency"));
    }

    [Fact]
    public void CreateHolding_Base_Currency_Passes_When_Configured()
    {
        var result = new CreateHoldingValidator(baseCurrency: "USD").Validate(ValidCoinRequest());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateHolding_Rejects_Oversized_Free_Text_Fields()
    {
        var request = ValidCoinRequest() with
        {
            Dealer = new string('d', 201),
            StorageLocation = new string('s', 201),
            SerialNumber = new string('n', 101),
            Notes = new string('x', 2001),
        };
        var result = new CreateHoldingValidator().Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Should().Contain([
            "Dealer",
            "StorageLocation",
            "SerialNumber",
            "Notes"]);
    }

    [Fact]
    public void CreateHolding_GenericBarWithoutCoinTypeId_Passes()
    {
        var bar = new CreateHoldingRequest
        {
            ItemForm = ItemForm.Bar,
            Quantity = 10,
            PurchasePricePerUnit = new MoneyInput(0.80m, "USD"),
            PurchaseDate = DateTimeOffset.UtcNow.AddDays(-1),
        };

        var result = new CreateHoldingValidator().Validate(bar);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(ItemForm.Round)]
    [InlineData(ItemForm.JunkSilver)]
    [InlineData(ItemForm.Scrap)]
    [InlineData(ItemForm.Jewelry)]
    [InlineData(ItemForm.Bar)]
    [InlineData(ItemForm.Ingot)]
    public void CreateHolding_CoinTypeIdRequirement_FollowsItemForm(ItemForm form)
    {
        var request = new CreateHoldingRequest
        {
            ItemForm = form,
            Quantity = 1,
            PurchasePricePerUnit = new MoneyInput(10m, "USD"),
            PurchaseDate = DateTimeOffset.UtcNow.AddDays(-1),
        };

        var result = new CreateHoldingValidator().Validate(request);

        if (form is ItemForm.Coin or ItemForm.Ingot)
        {
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "CoinTypeId");
        }
        else
        {
            result.IsValid.Should().BeTrue();
        }
    }

    [Fact]
    public void CreateHolding_QuantityBelowOne_Fails()
    {
        var request = ValidCoinRequest() with { Quantity = 0 };
        new CreateHoldingValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateHolding_ZeroPrice_Fails()
    {
        var request = new CreateHoldingRequest
        {
            CoinTypeId = 1,
            ItemForm = ItemForm.Coin,
            Quantity = 1,
            PurchasePricePerUnit = new MoneyInput(0m, "USD"),
            PurchaseDate = DateTimeOffset.UtcNow.AddDays(-1),
        };
        new CreateHoldingValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateHolding_FuturePurchaseDate_Fails()
    {
        // Frozen clock: 2026-01-01. A purchase dated the next day is future.
        var validator = new CreateHoldingValidator(
            new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        var request = new CreateHoldingRequest
        {
            CoinTypeId = 1,
            ItemForm = ItemForm.Coin,
            Quantity = 1,
            PurchasePricePerUnit = new MoneyInput(10m, "USD"),
            PurchaseDate = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PurchaseDate");
    }

    [Fact]
    public void CreateHolding_MalformedCurrency_Fails()
    {
        var request = new CreateHoldingRequest
        {
            CoinTypeId = 1,
            ItemForm = ItemForm.Coin,
            Quantity = 1,
            PurchasePricePerUnit = new MoneyInput(10m, "usd"),
            PurchaseDate = DateTimeOffset.UtcNow.AddDays(-1),
        };
        var result = new CreateHoldingValidator().Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PurchasePricePerUnit.Currency");
    }

    // ------------------------------------------------------- Register

    [Fact]
    public void Register_ValidRequest_Passes()
    {
        var result = new RegisterValidator().Validate(
            new RegisterRequest("collector@example.org", "a-very-original-passphrase", "Alex"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Register_ShortPassword_Fails()
    {
        var result = new RegisterValidator().Validate(
            new RegisterRequest("collector@example.org", "short"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public void Register_CommonPassword_Fails()
    {
        var result = new RegisterValidator().Validate(
            new RegisterRequest("collector@example.org", "password12345"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public void Register_CommonPasswordIsCaseInsensitive_Fails()
    {
        var result = new RegisterValidator().Validate(
            new RegisterRequest("collector@example.org", "QWERTYUIOPAS"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Register_BadEmail_Fails()
    {
        var result = new RegisterValidator().Validate(
            new RegisterRequest("not-an-email", "a-very-original-passphrase"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    // ------------------------------------------- SubmitIdentification

    private static byte[] Bytes(int size) => new byte[size];

    [Fact]
    public void SubmitIdentification_ValidImages_Pass()
    {
        var request = new SubmitIdentificationRequest(
            Bytes(SubmitIdentificationValidator.MinImageBytes + 1),
            Bytes(SubmitIdentificationValidator.MinImageBytes + 1));

        new SubmitIdentificationValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SubmitIdentification_MissingReverse_Fails()
    {
        var request = new SubmitIdentificationRequest(
            Bytes(SubmitIdentificationValidator.MinImageBytes + 1),
            reverseImage: null);

        var result = new SubmitIdentificationValidator().Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ReverseImage");
    }

    [Fact]
    public void SubmitIdentification_TooSmallObverse_Fails()
    {
        var request = new SubmitIdentificationRequest(
            Bytes(SubmitIdentificationValidator.MinImageBytes),
            Bytes(SubmitIdentificationValidator.MinImageBytes + 1));

        var result = new SubmitIdentificationValidator().Validate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SubmitIdentification_TooLargeReverse_Fails()
    {
        var request = new SubmitIdentificationRequest(
            Bytes(SubmitIdentificationValidator.MinImageBytes + 1),
            Bytes(SubmitIdentificationValidator.MaxImageBytes));

        new SubmitIdentificationValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SubmitIdentification_TooLargeEdge_Fails()
    {
        var request = new SubmitIdentificationRequest(
            Bytes(SubmitIdentificationValidator.MinImageBytes + 1),
            Bytes(SubmitIdentificationValidator.MinImageBytes + 1),
            Bytes(SubmitIdentificationValidator.MaxImageBytes));

        new SubmitIdentificationValidator().Validate(request).IsValid.Should().BeFalse();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
