using FluentValidation;
using Mintmark.Application.Dtos;

namespace Mintmark.Application.Validators;

/// <summary>Validates identification submissions: both required photos present and within size bounds.</summary>
public sealed class SubmitIdentificationValidator : AbstractValidator<SubmitIdentificationRequest>
{
    /// <summary>Minimum accepted image size: strictly greater than 10 KB.</summary>
    public const int MinImageBytes = 10 * 1024;

    /// <summary>Maximum accepted image size: strictly less than 15 MB.</summary>
    public const int MaxImageBytes = 15 * 1024 * 1024;

    /// <summary>Initializes the validator.</summary>
    public SubmitIdentificationValidator()
    {
        RuleFor(x => x.ObverseImage)
            .NotNull()
            .WithMessage("An obverse (front) image is required.")
            .Must(bytes => bytes is not null && bytes.Length > MinImageBytes)
            .WithMessage($"Obverse image must be larger than {MinImageBytes} bytes (10 KB).")
            .Must(bytes => bytes is null || bytes.Length < MaxImageBytes)
            .WithMessage($"Obverse image must be smaller than {MaxImageBytes} bytes (15 MB).");

        RuleFor(x => x.ReverseImage)
            .NotNull()
            .WithMessage("A reverse (back) image is required.")
            .Must(bytes => bytes is not null && bytes.Length > MinImageBytes)
            .WithMessage($"Reverse image must be larger than {MinImageBytes} bytes (10 KB).")
            .Must(bytes => bytes is null || bytes.Length < MaxImageBytes)
            .WithMessage($"Reverse image must be smaller than {MaxImageBytes} bytes (15 MB).");

        RuleFor(x => x.EdgeImage)
            .Must(bytes => bytes is null || bytes.Length < MaxImageBytes)
            .WithMessage($"Edge image must be smaller than {MaxImageBytes} bytes (15 MB).");
    }
}
