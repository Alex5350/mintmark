namespace Mintmark.Domain.Entities;

/// <summary>
/// Optional 1:1 grading record for a <see cref="Holding"/>: service,
/// numeric grade, designations, cert number, label pedigree and a
/// verification URL.
/// </summary>
public sealed class Grading
{
    /// <summary>Parameterless constructor for EF Core materialization only.</summary>
    private Grading()
    {
    }

    private Grading(HoldingId holdingId)
    {
        HoldingId = holdingId;
    }

    /// <summary>Gets the holding this record grades.</summary>
    public HoldingId HoldingId { get; private set; }

    /// <summary>Gets the grading service (<c>Raw</c> when not service-graded).</summary>
    public GradingService Service { get; private set; }

    /// <summary>Gets the numeric grade (1–70), or <c>null</c> for raw items.</summary>
    public int? NumericGrade { get; private set; }

    /// <summary>Gets cameo/release designations.</summary>
    public GradingDesignation Designations { get; private set; }

    /// <summary>Gets the certification number, if service-graded.</summary>
    public string? CertificationNumber { get; private set; }

    /// <summary>Gets the label pedigree, if any.</summary>
    public string? LabelPedigree { get; private set; }

    /// <summary>Gets the verification URL, if any.</summary>
    public string? VerificationUrl { get; private set; }

    /// <summary>Creates a grading record, enforcing its invariants.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown when a raw item carries a numeric grade, or a graded numeric
    /// value falls outside 1–70.
    /// </exception>
    public static Grading Create(
        HoldingId holdingId,
        GradingService service,
        int? numericGrade = null,
        GradingDesignation designations = GradingDesignation.None,
        string? certificationNumber = null,
        string? labelPedigree = null,
        string? verificationUrl = null)
    {
        if (service == GradingService.Raw && numericGrade.HasValue)
        {
            throw new ArgumentException("A raw item cannot carry a numeric grade.", nameof(numericGrade));
        }

        if (service != GradingService.Raw)
        {
            if (!numericGrade.HasValue)
            {
                throw new ArgumentException(
                    $"A {service} grading requires a numeric grade (1-70).", nameof(numericGrade));
            }

            if (numericGrade.Value is < 1 or > 70)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(numericGrade), numericGrade, "Numeric grade must be between 1 and 70.");
            }
        }

        return new Grading(holdingId)
        {
            Service = service,
            NumericGrade = numericGrade,
            Designations = designations,
            CertificationNumber = certificationNumber,
            LabelPedigree = labelPedigree,
            VerificationUrl = verificationUrl,
        };
    }
}
