using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Financial.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Financial.GetMappingSettings;

public sealed class GetMappingSettingsHandler(IBineshDbContext db)
    : IRequestHandler<GetMappingSettingsQuery, MappingSettingsDto>
{
    public async Task<MappingSettingsDto> Handle(GetMappingSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await db.FinancialMappingSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("FinancialMappingSettings", "default");

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
