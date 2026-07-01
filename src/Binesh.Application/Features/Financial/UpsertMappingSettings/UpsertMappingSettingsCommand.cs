using Binesh.Application.Features.Financial.Shared;
using Binesh.Domain.Financial;
using MediatR;

namespace Binesh.Application.Features.Financial.UpsertMappingSettings;

/// <summary>
/// Whole-document replace of the singleton mapping settings. Creates if
/// absent, overwrites all categories if present. Any list omitted from the
/// body defaults to empty.
/// </summary>
public sealed record UpsertMappingSettingsCommand(
    IReadOnlyList<DetailedItem>? OperationalCost,
    IReadOnlyList<DetailedItem>? Payables,
    IReadOnlyList<DetailedItem>? ToCalculateSales,
    IReadOnlyList<DetailedItem>? ToCalculateLiquidity,
    IReadOnlyList<DetailedItem>? ToCalculateGrossProfitLoss,
    IReadOnlyList<DetailedItem>? ToCalculateOperatingProfitLoss,
    IReadOnlyList<DetailedItem>? ToCalculateProfitLossBeforTax,
    IReadOnlyList<DetailedItem>? ToCalculateNetProfitLoss,
    IReadOnlyList<DetailedItem>? ToCalculateAccumulatedProfitLoss,
    IReadOnlyList<DetailedItem>? ToCalculateEquity)
    : IRequest<MappingSettingsDto>;
