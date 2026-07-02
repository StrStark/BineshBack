using Binesh.Application.Abstractions;
using Binesh.Application.Features.Financial.Shared;
using Binesh.Domain.Financial;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Financial.UpsertMappingSettings;

public sealed class UpsertMappingSettingsHandler(IBineshDbContext db, ITenantContext tenantContext)
    : IRequestHandler<UpsertMappingSettingsCommand, MappingSettingsDto>
{
    public async Task<MappingSettingsDto> Handle(UpsertMappingSettingsCommand request, CancellationToken cancellationToken)
    {
        var companyId = tenantContext.RequireCompanyId();
        var existing = await db.FinancialMappingSettings.SingleOrDefaultAsync(cancellationToken);

        var operationalCost = request.OperationalCost ?? [];
        var payables = request.Payables ?? [];
        var toCalculateSales = request.ToCalculateSales ?? [];
        var toCalculateLiquidity = request.ToCalculateLiquidity ?? [];
        var toCalculateGrossProfitLoss = request.ToCalculateGrossProfitLoss ?? [];
        var toCalculateOperatingProfitLoss = request.ToCalculateOperatingProfitLoss ?? [];
        var toCalculateProfitLossBeforTax = request.ToCalculateProfitLossBeforTax ?? [];
        var toCalculateNetProfitLoss = request.ToCalculateNetProfitLoss ?? [];
        var toCalculateAccumulatedProfitLoss = request.ToCalculateAccumulatedProfitLoss ?? [];
        var toCalculateEquity = request.ToCalculateEquity ?? [];

        FinancialMappingSettings settings;
        if (existing is null)
        {
            settings = FinancialMappingSettings.Create(
                companyId,
                operationalCost, payables, toCalculateSales, toCalculateLiquidity,
                toCalculateGrossProfitLoss, toCalculateOperatingProfitLoss,
                toCalculateProfitLossBeforTax, toCalculateNetProfitLoss,
                toCalculateAccumulatedProfitLoss, toCalculateEquity);
            db.FinancialMappingSettings.Add(settings);
        }
        else
        {
            existing.Replace(
                operationalCost, payables, toCalculateSales, toCalculateLiquidity,
                toCalculateGrossProfitLoss, toCalculateOperatingProfitLoss,
                toCalculateProfitLossBeforTax, toCalculateNetProfitLoss,
                toCalculateAccumulatedProfitLoss, toCalculateEquity);
            settings = existing;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new MappingSettingsDto(
            settings.Id,
            settings.OperationalCost,
            settings.Payables,
            settings.ToCalculateSales,
            settings.ToCalculateLiquidity,
            settings.ToCalculateGrossProfitLoss,
            settings.ToCalculateOperatingProfitLoss,
            settings.ToCalculateProfitLossBeforTax,
            settings.ToCalculateNetProfitLoss,
            settings.ToCalculateAccumulatedProfitLoss,
            settings.ToCalculateEquity);
    }
}
