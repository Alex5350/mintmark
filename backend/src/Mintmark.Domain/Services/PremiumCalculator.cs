using Mintmark.Domain.Entities;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Domain.Services;

/// <summary>
/// The tunable weights of the rules-based collectible premium model
/// (ADR 0007). All values are data: overriding them changes outputs without a
/// deployment, and golden tests freeze them so any change is a deliberate,
/// reviewed diff.
/// </summary>
public sealed record PremiumFactorTable
{
    /// <summary>Gets a shared default table instance (the ADR 0007 weights).</summary>
    public static PremiumFactorTable Default { get; } = new();

    // Mintage rarity tiers (mintage == null -> unknown, neutral).
    /// <summary>Factor for unknown mintage.</summary>
    public decimal MintageUnknown { get; init; } = 1.0m;

    /// <summary>Factor for mintage at or below 5,000.</summary>
    public decimal MintageAtMost5000 { get; init; } = 3.0m;

    /// <summary>Factor for mintage at or below 25,000.</summary>
    public decimal MintageAtMost25000 { get; init; } = 2.0m;

    /// <summary>Factor for mintage at or below 100,000.</summary>
    public decimal MintageAtMost100000 { get; init; } = 1.5m;

    /// <summary>Factor for mintage at or below 1,000,000.</summary>
    public decimal MintageAtMost1Million { get; init; } = 1.15m;

    /// <summary>Factor for mintage above 1,000,000.</summary>
    public decimal MintageCommon { get; init; } = 1.0m;

    // Primary finish (ADR 0003: the primary carries the main finish weight).
    /// <summary>Factor for business strike.</summary>
    public decimal FinishBusinessStrike { get; init; } = 1.0m;

    /// <summary>Factor for bullion uncirculated.</summary>
    public decimal FinishBullionUncirculated { get; init; } = 1.0m;

    /// <summary>Factor for proof.</summary>
    public decimal FinishProof { get; init; } = 1.6m;

    /// <summary>Factor for reverse proof.</summary>
    public decimal FinishReverseProof { get; init; } = 1.8m;

    /// <summary>Factor for burnished.</summary>
    public decimal FinishBurnished { get; init; } = 1.3m;

    /// <summary>Factor for matte proof.</summary>
    public decimal FinishMatteProof { get; init; } = 1.5m;

    /// <summary>Factor for unknown finish.</summary>
    public decimal FinishUnknown { get; init; } = 1.0m;

    // Finish attribute flags: stacking multipliers on the primary.
    /// <summary>Factor for the HighRelief flag.</summary>
    public decimal FlagHighRelief { get; init; } = 1.15m;

    /// <summary>Factor for the Enhanced flag.</summary>
    public decimal FlagEnhanced { get; init; } = 1.0m;

    /// <summary>Factor for the Colorized flag.</summary>
    public decimal FlagColorized { get; init; } = 1.05m;

    /// <summary>Factor for the Antiqued flag.</summary>
    public decimal FlagAntiqued { get; init; } = 1.1m;

    /// <summary>Factor for the FirstStrike flag.</summary>
    public decimal FlagFirstStrike { get; init; } = 1.0m;

    // Grade and designation.
    /// <summary>Factor for raw / ungraded items.</summary>
    public decimal GradeRaw { get; init; } = 1.0m;

    /// <summary>Factor for MS69 / PR69.</summary>
    public decimal Grade69 { get; init; } = 1.3m;

    /// <summary>Factor for MS70 / PR70.</summary>
    public decimal Grade70 { get; init; } = 1.6m;

    /// <summary>Factor for other graded values.</summary>
    public decimal GradeOther { get; init; } = 1.0m;

    /// <summary>Factor additionally applied for Ultra/Deep Cameo designations.</summary>
    public decimal DesignationCameo { get; init; } = 1.1m;

    // Series demand tier (reference data per series).
    /// <summary>Factor for low demand series.</summary>
    public decimal DemandLow { get; init; } = 1.0m;

    /// <summary>Factor for medium demand series.</summary>
    public decimal DemandMedium { get; init; } = 1.15m;

    /// <summary>Factor for high demand series.</summary>
    public decimal DemandHigh { get; init; } = 1.4m;

    // Age band.
    /// <summary>Factor for coins struck before the threshold year.</summary>
    public decimal AgePre1936 { get; init; } = 1.25m;

    /// <summary>Factor for modern coins.</summary>
    public decimal AgeModern { get; init; } = 1.0m;

    /// <summary>Gets the age-band threshold year (coins struck strictly before it are 'pre-1936').</summary>
    public int AgeThresholdYear { get; init; } = 1936;

    // Confidence band.
    /// <summary>Gets the base confidence half-width (as a fraction of the estimate).</summary>
    public decimal ConfidenceBaseHalfWidth { get; init; } = 0.15m;

    /// <summary>Gets the extra half-width per applied factor beyond <see cref="ConfidenceIncludedFactorCount"/>.</summary>
    public decimal ConfidencePerAdditionalFactor { get; init; } = 0.05m;

    /// <summary>Gets how many applied (non-neutral) factors are covered by the base half-width.</summary>
    public int ConfidenceIncludedFactorCount { get; init; } = 3;
}

/// <summary>One line of the premium explanation: which factor, its multiplier, and why it applied.</summary>
/// <param name="FactorName">Stable machine name of the factor (e.g. <c>MintageTier</c>).</param>
/// <param name="Multiplier">The multiplier this factor contributed.</param>
/// <param name="Rationale">Human-readable justification citing the input data.</param>
public sealed record PremiumFactor(string FactorName, decimal Multiplier, string Rationale);

/// <summary>
/// The result of the premium rules: the total multiplier, the itemized
/// breakdown, and the confidence band. Applying it to a melt value yields the
/// collectible estimate and the premium.
/// </summary>
public sealed record PremiumEstimate
{
    /// <summary>Initializes the estimate.</summary>
    public PremiumEstimate(
        decimal multiplier,
        IReadOnlyList<PremiumFactor> factors,
        decimal confidenceHalfWidth)
    {
        Multiplier = multiplier;
        Factors = factors;
        ConfidenceHalfWidth = confidenceHalfWidth;
        BandLowFraction = Math.Max(0m, 1m - confidenceHalfWidth);
        BandHighFraction = 1m + confidenceHalfWidth;
    }

    /// <summary>Gets the product of all factor multipliers: collectible = melt x Multiplier.</summary>
    public decimal Multiplier { get; }

    /// <summary>Gets every applied factor in deterministic order (mintage, finish, flags, grade, designation, demand, age).</summary>
    public IReadOnlyList<PremiumFactor> Factors { get; }

    /// <summary>Gets the confidence half-width as a fraction of the estimate.</summary>
    public decimal ConfidenceHalfWidth { get; }

    /// <summary>Gets the lower band bound as a fraction of the estimate (in [0, 1]).</summary>
    public decimal BandLowFraction { get; }

    /// <summary>Gets the upper band bound as a fraction of the estimate (&gt;= 1).</summary>
    public decimal BandHighFraction { get; }

    /// <summary>Gets how many factors actually moved the multiplier (non-neutral).</summary>
    public int AppliedFactorCount => Factors.Count(f => f.Multiplier != 1.0m);

    /// <summary>
    /// Applies the estimate to a melt value:
    /// collectible = melt x Π(factors); premium = collectible − melt.
    /// </summary>
    public CollectibleEstimate ApplyTo(Money meltValue) => new(meltValue, meltValue * Multiplier);
}

/// <summary>The collectible decomposition of one melt value under a <see cref="PremiumEstimate"/>.</summary>
public sealed record CollectibleEstimate
{
    /// <summary>Initializes the estimate parts.</summary>
    public CollectibleEstimate(Money melt, Money collectible)
    {
        Melt = melt;
        Collectible = collectible;
    }

    /// <summary>Gets the melt value the estimate started from.</summary>
    public Money Melt { get; }

    /// <summary>Gets the collectible estimate: melt x premium multiplier.</summary>
    public Money Collectible { get; }

    /// <summary>Gets the numismatic premium: collectible − melt.</summary>
    public Money Premium => Collectible - Melt;
}

/// <summary>
/// The rules-based collectible premium calculator (ADR 0007). Every estimate
/// is a product of inspectable factors with an itemized breakdown; no special
/// cases — the canonical divergence (low-mintage reverse proof vs common
/// bullion) falls out of the factors alone.
/// </summary>
public sealed class PremiumCalculator
{
    /// <summary>Initializes the calculator with a table (defaults when null).</summary>
    public PremiumCalculator(PremiumFactorTable? table = null) => Table = table ?? PremiumFactorTable.Default;

    /// <summary>Gets the weights in force.</summary>
    public PremiumFactorTable Table { get; }

    /// <summary>
    /// Computes the premium multiplier and its breakdown for one coin.
    /// Factors are emitted in a deterministic order: mintage tier, finish
    /// primary, finish attribute flags, grade, cameo designation, series
    /// demand tier, age band. The confidence band widens by
    /// <see cref="PremiumFactorTable.ConfidencePerAdditionalFactor"/> per
    /// applied (non-neutral) factor beyond the first
    /// <see cref="PremiumFactorTable.ConfidenceIncludedFactorCount"/>.
    /// </summary>
    public PremiumEstimate Estimate(CoinType coinType, Grading? grading, SeriesDemandTier seriesDemandTier)
    {
        ArgumentNullException.ThrowIfNull(coinType);

        var t = Table;
        var factors = new List<PremiumFactor>();

        // 1. Mintage rarity tier.
        var (mintageMultiplier, mintageRationale) = coinType.Mintage switch
        {
            null => (t.MintageUnknown, "Mintage unknown — neutral"),
            <= 5000 => (t.MintageAtMost5000, $"Mintage {coinType.Mintage:N0} at or below 5,000"),
            <= 25000 => (t.MintageAtMost25000, $"Mintage {coinType.Mintage:N0} at or below 25,000"),
            <= 100000 => (t.MintageAtMost100000, $"Mintage {coinType.Mintage:N0} at or below 100,000"),
            <= 1000000 => (t.MintageAtMost1Million, $"Mintage {coinType.Mintage:N0} at or below 1,000,000"),
            _ => (t.MintageCommon, $"Mintage {coinType.Mintage:N0} above 1,000,000"),
        };
        factors.Add(new PremiumFactor("MintageTier", mintageMultiplier, mintageRationale));

        // 2. Finish primary.
        var finishMultiplier = coinType.Finish switch
        {
            FinishPrimary.BusinessStrike => t.FinishBusinessStrike,
            FinishPrimary.BullionUncirculated => t.FinishBullionUncirculated,
            FinishPrimary.Proof => t.FinishProof,
            FinishPrimary.ReverseProof => t.FinishReverseProof,
            FinishPrimary.Burnished => t.FinishBurnished,
            FinishPrimary.MatteProof => t.FinishMatteProof,
            _ => t.FinishUnknown,
        };
        factors.Add(new PremiumFactor("FinishPrimary", finishMultiplier, $"Primary finish {coinType.Finish}"));

        // 3. Finish attribute flags (ADR 0003), in declaration order.
        if ((coinType.FinishAttributes & FinishAttribute.HighRelief) != 0)
        {
            factors.Add(new PremiumFactor("FinishAttributeHighRelief", t.FlagHighRelief, "HighRelief attribute present"));
        }

        if ((coinType.FinishAttributes & FinishAttribute.Enhanced) != 0)
        {
            factors.Add(new PremiumFactor("FinishAttributeEnhanced", t.FlagEnhanced, "Enhanced attribute present"));
        }

        if ((coinType.FinishAttributes & FinishAttribute.Colorized) != 0)
        {
            factors.Add(new PremiumFactor("FinishAttributeColorized", t.FlagColorized, "Colorized attribute present"));
        }

        if ((coinType.FinishAttributes & FinishAttribute.Antiqued) != 0)
        {
            factors.Add(new PremiumFactor("FinishAttributeAntiqued", t.FlagAntiqued, "Antiqued attribute present"));
        }

        if ((coinType.FinishAttributes & FinishAttribute.FirstStrike) != 0)
        {
            factors.Add(new PremiumFactor("FinishAttributeFirstStrike", t.FlagFirstStrike, "FirstStrike attribute present"));
        }

        // 4. Grade.
        var (gradeMultiplier, gradeRationale) = grading switch
        {
            null => (t.GradeRaw, "Raw / ungraded"),
            { Service: GradingService.Raw } => (t.GradeRaw, "Raw (no service grade)"),
            { NumericGrade: 70 } => (t.Grade70, $"Graded {grading.Service} {grading.NumericGrade}"),
            { NumericGrade: 69 } => (t.Grade69, $"Graded {grading.Service} {grading.NumericGrade}"),
            { NumericGrade: not null } => (t.GradeOther, $"Graded {grading.Service} {grading.NumericGrade}"),
            _ => (t.GradeRaw, "Raw / ungraded"),
        };
        factors.Add(new PremiumFactor("Grade", gradeMultiplier, gradeRationale));

        // 5. Cameo designation (additionally applied on top of the grade factor).
        if (grading is not null
            && (grading.Designations & (GradingDesignation.UltraCameo | GradingDesignation.DeepCameo)) != 0)
        {
            factors.Add(new PremiumFactor("DesignationCameo", t.DesignationCameo, $"Cameo designation {grading.Designations}"));
        }

        // 6. Series demand tier.
        var (demandMultiplier, demandRationale) = seriesDemandTier switch
        {
            SeriesDemandTier.High => (t.DemandHigh, "Series demand tier High"),
            SeriesDemandTier.Medium => (t.DemandMedium, "Series demand tier Medium"),
            _ => (t.DemandLow, "Series demand tier Low"),
        };
        factors.Add(new PremiumFactor("SeriesDemand", demandMultiplier, demandRationale));

        // 7. Age band.
        var isPreThreshold = coinType.Year < t.AgeThresholdYear;
        factors.Add(new PremiumFactor(
            "Age",
            isPreThreshold ? t.AgePre1936 : t.AgeModern,
            isPreThreshold
                ? $"Struck {coinType.Year}, before {t.AgeThresholdYear}"
                : $"Struck {coinType.Year}, {t.AgeThresholdYear} or later"));

        var multiplier = factors.Aggregate(1.0m, (acc, factor) => acc * factor.Multiplier);
        var applied = factors.Count(f => f.Multiplier != 1.0m);
        var halfWidth = t.ConfidenceBaseHalfWidth
            + (t.ConfidencePerAdditionalFactor * Math.Max(0, applied - t.ConfidenceIncludedFactorCount));

        return new PremiumEstimate(multiplier, factors, halfWidth);
    }
}
