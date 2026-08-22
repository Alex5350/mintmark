using Mintmark.Domain;

namespace Mintmark.Application.Dtos;

/// <summary>Catalog detail for one CoinType row.</summary>
/// <param name="Id">CoinType identifier.</param>
/// <param name="SeriesId">Owning series.</param>
/// <param name="SeriesName">Series display name.</param>
/// <param name="MintId">Striking mint.</param>
/// <param name="MintName">Mint display name.</param>
/// <param name="Year">Year of issue.</param>
/// <param name="Name">Catalog display name.</param>
/// <param name="Metal">The series metal.</param>
/// <param name="Fineness">Fineness (e.g. 0.999).</param>
/// <param name="GrossWeightGrams">Gross weight in grams.</param>
/// <param name="ActualMetalWeightTroyOz">ASW/AGW in troy ounces — the only number melt uses.</param>
/// <param name="DiameterMillimeters">Diameter, when published.</param>
/// <param name="ThicknessMillimeters">Thickness, when published.</param>
/// <param name="Edge">Edge type, when known.</param>
/// <param name="Finish">Primary finish.</param>
/// <param name="FinishAttributes">Finish attribute flags.</param>
/// <param name="Mintage">Mintage figure, or null when unknown/disputed.</param>
/// <param name="SourceUrl">Required source of the specification figures.</param>
/// <param name="KmNumber">Krause KM number, when any.</param>
/// <param name="RedBookReference">Red Book reference, when any.</param>
public sealed record CoinTypeDetail(
    CoinTypeId Id,
    SeriesId SeriesId,
    string SeriesName,
    MintId MintId,
    string MintName,
    int Year,
    string Name,
    MetalKind Metal,
    decimal Fineness,
    decimal GrossWeightGrams,
    decimal ActualMetalWeightTroyOz,
    decimal? DiameterMillimeters,
    decimal? ThicknessMillimeters,
    EdgeType? Edge,
    FinishPrimary Finish,
    FinishAttribute FinishAttributes,
    long? Mintage,
    string SourceUrl,
    string? KmNumber,
    string? RedBookReference);

/// <summary>Summary of one series for catalog browsing.</summary>
/// <param name="Id">Series identifier.</param>
/// <param name="Name">Series display name.</param>
/// <param name="MintName">Issuing mint.</param>
/// <param name="Metal">The series metal.</param>
/// <param name="StartYear">First year of issue, when known.</param>
/// <param name="EndYear">Last year of issue, when known.</param>
/// <param name="CoinTypeCount">How many catalog rows the series has.</param>
public sealed record SeriesSummary(
    SeriesId Id,
    string Name,
    string MintName,
    MetalKind Metal,
    int? StartYear,
    int? EndYear,
    int CoinTypeCount);

/// <summary>Summary of one mint for catalog browsing.</summary>
/// <param name="Id">Mint identifier.</param>
/// <param name="Name">Mint display name.</param>
/// <param name="Country">Country.</param>
/// <param name="CountryCode">Two-letter ISO country code.</param>
/// <param name="MintMarks">The mint's marks (W, S, P, D, Mo, ...).</param>
/// <param name="FoundedYear">Founding year, when known.</param>
/// <param name="IsActive">Whether the mint is currently striking.</param>
public sealed record MintSummary(
    MintId Id,
    string Name,
    string Country,
    string CountryCode,
    IReadOnlyList<string> MintMarks,
    int? FoundedYear,
    bool IsActive);
