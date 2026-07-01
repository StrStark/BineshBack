using Binesh.Domain.Financial;

namespace Binesh.Application.Features.Financial.Shared;

/// <summary>
/// Wire shape for <see cref="FinancialMappingSettings"/>. Each category is a
/// list of <see cref="DetailedItem"/> (kept for parity with the old schema;
/// only Title is consumed by the panel aggregator today).
/// </summary>
public sealed record MappingSettingsDto(
    Guid Id,
    IReadOnlyList<DetailedItem> OperationalCost,
    IReadOnlyList<DetailedItem> Payables,
    IReadOnlyList<DetailedItem> ToCalculateSales,
    IReadOnlyList<DetailedItem> ToCalculateLiquidity,
    IReadOnlyList<DetailedItem> ToCalculateGrossProfitLoss,
    IReadOnlyList<DetailedItem> ToCalculateOperatingProfitLoss,
    IReadOnlyList<DetailedItem> ToCalculateProfitLossBeforTax,
    IReadOnlyList<DetailedItem> ToCalculateNetProfitLoss,
    IReadOnlyList<DetailedItem> ToCalculateAccumulatedProfitLoss,
    IReadOnlyList<DetailedItem> ToCalculateEquity);
