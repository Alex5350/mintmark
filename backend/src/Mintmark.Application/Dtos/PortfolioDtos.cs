using Mintmark.Domain;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Application.Dtos;

/// <summary>Portfolio value attributed to one metal.</summary>
/// <param name="Metal">The metal.</param>
/// <param name="Value">The value attributed to it.</param>
/// <param name="Weight">The allocation weight (fraction of the total, 0..1).</param>
public sealed record MetalAllocationDto(MetalKind Metal, Money Value, decimal Weight);

/// <summary>Portfolio value attributed to one series.</summary>
/// <param name="SeriesId">The series.</param>
/// <param name="SeriesName">Series display name.</param>
/// <param name="Value">The value attributed to it.</param>
/// <param name="Weight">The allocation weight (fraction of the total, 0..1).</param>
public sealed record SeriesAllocationDto(SeriesId SeriesId, string SeriesName, Money Value, decimal Weight);

/// <summary>Portfolio rollup: totals, cost basis vs current value, and allocations by metal and top series.</summary>
/// <param name="HoldingCount">How many holdings are included.</param>
/// <param name="CostBasis">Total effective cost.</param>
/// <param name="CurrentValue">Total current value.</param>
/// <param name="UnrealizedPct">Unrealized gain/loss percentage (12.34 means +12.34%).</param>
/// <param name="ByMetal">Allocation by metal (weights sum to exactly 1).</param>
/// <param name="BySeries">Top series by value (weights are fractions of the total).</param>
public sealed record PortfolioRollup(
    int HoldingCount,
    Money CostBasis,
    Money CurrentValue,
    decimal UnrealizedPct,
    IReadOnlyList<MetalAllocationDto> ByMetal,
    IReadOnlyList<SeriesAllocationDto> BySeries);
