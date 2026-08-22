namespace Mintmark.Domain;

/// <summary>Precious metals priced by the system. All four ship in v1 even though only gold and silver have features.</summary>
public enum MetalKind
{
    /// <summary>Gold.</summary>
    Gold,

    /// <summary>Silver.</summary>
    Silver,

    /// <summary>Platinum.</summary>
    Platinum,

    /// <summary>Palladium.</summary>
    Palladium,
}

/// <summary>Physical form of a held item.</summary>
public enum ItemForm
{
    /// <summary>A struck coin.</summary>
    Coin,

    /// <summary>A round (coin-like, no legal tender status).</summary>
    Round,

    /// <summary>A bar.</summary>
    Bar,

    /// <summary>An ingot.</summary>
    Ingot,

    /// <summary>90% (or similar) constitutional silver traded by face value.</summary>
    JunkSilver,

    /// <summary>Scrap metal.</summary>
    Scrap,

    /// <summary>Jewelry.</summary>
    Jewelry,
}

/// <summary>
/// Primary finish of a coin. ADR 0003: finish is a primary value plus
/// independent attribute flags, not a flat enum — a high-relief reverse proof
/// is two facts, not one.
/// </summary>
public enum FinishPrimary
{
    /// <summary>Standard circulation strike.</summary>
    BusinessStrike,

    /// <summary>Bullion uncirculated (BU).</summary>
    BullionUncirculated,

    /// <summary>Mirrored fields, frosted devices.</summary>
    Proof,

    /// <summary>Frosted fields, mirrored devices.</summary>
    ReverseProof,

    /// <summary>Soft matte sheen, no mirroring.</summary>
    Burnished,

    /// <summary>Granular non-reflective surfaces on fields and devices.</summary>
    MatteProof,

    /// <summary>Finish could not be determined.</summary>
    Unknown,
}

/// <summary>Stackable finish attribute flags (ADR 0003).</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "The name FinishAttribute is fixed by the binding docs/domain-model.md.")]
[Flags]
public enum FinishAttribute
{
    /// <summary>No attributes.</summary>
    None = 0,

    /// <summary>Steep field-to-device transition.</summary>
    HighRelief = 1 << 0,

    /// <summary>Enhanced (decorated) surfaces.</summary>
    Enhanced = 1 << 1,

    /// <summary>Colorized.</summary>
    Colorized = 1 << 2,

    /// <summary>Antiqued.</summary>
    Antiqued = 1 << 3,

    /// <summary>First strike (only from packaging/slab labeling).</summary>
    FirstStrike = 1 << 4,
}

/// <summary>Which side of a holding a photo shows.</summary>
public enum CoinSide
{
    /// <summary>Front.</summary>
    Obverse,

    /// <summary>Back.</summary>
    Reverse,

    /// <summary>Edge.</summary>
    Edge,

    /// <summary>The slab (holder), not the coin itself.</summary>
    Slab,

    /// <summary>Anything else.</summary>
    Other,
}

/// <summary>Edge characterization of a coin, when known.</summary>
public enum EdgeType
{
    /// <summary>Reeded (grooved) edge.</summary>
    Reeded,

    /// <summary>Edge carries lettering.</summary>
    Lettered,

    /// <summary>Smooth edge.</summary>
    Plain,

    /// <summary>Edge type unknown.</summary>
    Unknown,
}

/// <summary>Third-party grading services, plus <c>Raw</c> for ungraded items.</summary>
public enum GradingService
{
    /// <summary>Numismatic Guaranty Company.</summary>
    NGC,

    /// <summary>Professional Coin Grading Service.</summary>
    PCGS,

    /// <summary>ANACS.</summary>
    ANACS,

    /// <summary>ICG.</summary>
    ICG,

    /// <summary>Not graded by a service.</summary>
    Raw,
}

/// <summary>Cameo and release designations that carry a premium adjustment.</summary>
[Flags]
public enum GradingDesignation
{
    /// <summary>No designations.</summary>
    None = 0,

    /// <summary>Ultra Cameo.</summary>
    UltraCameo = 1 << 0,

    /// <summary>Deep Cameo.</summary>
    DeepCameo = 1 << 1,

    /// <summary>Early Releases.</summary>
    EarlyReleases = 1 << 2,

    /// <summary>First Releases.</summary>
    FirstReleases = 1 << 3,
}

/// <summary>Kind of valuation produced for a holding.</summary>
public enum ValuationType
{
    /// <summary>Metal value only: ASW/AGW x spot.</summary>
    Melt,

    /// <summary>Melt plus a rules-based numismatic premium (ADR 0007).</summary>
    Collectible,
}

/// <summary>Market demand tier of a series; an input to the premium rules, stored as reference data.</summary>
public enum SeriesDemandTier
{
    /// <summary>Low demand.</summary>
    Low,

    /// <summary>Medium demand.</summary>
    Medium,

    /// <summary>High demand.</summary>
    High,
}
