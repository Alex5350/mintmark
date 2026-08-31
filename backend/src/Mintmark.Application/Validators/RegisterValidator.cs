using System.Collections.Frozen;
using FluentValidation;
using Mintmark.Application.Dtos;

namespace Mintmark.Application.Validators;

/// <summary>
/// A tiny denylist of common passwords that also satisfy the length rule:
/// length alone is not strength. Case-insensitive. Kept deliberately small;
/// this is the Application-layer backstop.
/// </summary>
public static class CommonPasswordDenylist
{
    /// <summary>Gets the denied passwords, compared ordinally-ignore-case.</summary>
    public static readonly FrozenSet<string> Passwords = new[]
    {
        "123456789012",
        "123456789101",
        "password12345",
        "password1234!",
        "qwertyuiop12",
        "qwertyuiopas",
        "letmein12345",
        "welcome123456",
        "admin12345678",
        "iloveyou12345",
        "sunshine12345",
        "princess12345",
        "goldsilver123",
        "silvergold123",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Validates registration requests: email shape and password strength.</summary>
public sealed class RegisterValidator : AbstractValidator<RegisterRequest>
{
    /// <summary>Initializes the validator.</summary>
    public RegisterValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(12)
            .WithMessage("Password must be at least 12 characters.")
            .Must(password => !CommonPasswordDenylist.Passwords.Contains(password ?? string.Empty))
            .WithMessage("Password is too common; choose something less predictable.");
    }
}
