using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Domain.Financial;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Financial.GetFinancialPanel;

/// <summary>
/// Direct port of the legacy <c>FinantialApiController.GetFinantialSummary</c>.
///
/// <para><b>Buggy math preserved on purpose.</b> The original handler has at
/// least four known mistakes (Liability filter, profitMargin precedence,
/// ProfitLossBeforTax intermediate, several typos). Per user direction the
/// port matches behaviour exactly so panel numbers stay byte-equivalent
/// until business-logic cleanup at the end of the transformation. The
/// inline comments tagged <c>PARITY-BUG</c> point at each one.</para>
/// </summary>
public sealed class GetFinancialPanelHandler(IBineshDbContext db)
    : IRequestHandler<GetFinancialPanelQuery, GetFinancialPanelResponse>
{
    public async Task<GetFinancialPanelResponse> Handle(GetFinancialPanelQuery request, CancellationToken cancellationToken)
    {
        var settingsCount = await db.FinancialMappingSettings.CountAsync(cancellationToken);
        if (settingsCount == 0)
        {
            throw new NotFoundException("FinancialMappingSettings", "default");
        }
        if (settingsCount > 1)
        {
            throw new ConflictException("Multiple FinancialMappingSettings rows found; expected exactly one.");
        }

        var settings = await db.FinancialMappingSettings.AsNoTracking().SingleAsync(cancellationToken);
        var accounts = await db.FinancialEntries.AsNoTracking().ToListAsync(cancellationToken);

        long SumFor(IReadOnlyList<DetailedItem> mapping) =>
            accounts
                .Where(a => mapping.Any(m => m.Title == a.Name))
                .Sum(a => a.Credit - a.Debit);

        IReadOnlyList<PanelDetailedItem> DetailsFor(IReadOnlyList<DetailedItem> mapping) =>
            accounts
                .Where(a => mapping.Any(m => m.Title == a.Name))
                .Select(a => new PanelDetailedItem(a.Name, a.Credit - a.Debit))
                .ToList();

        var totalSale = SumFor(settings.ToCalculateSales);
        var liquidity = SumFor(settings.ToCalculateLiquidity);
        var operationalCosts = SumFor(settings.OperationalCost);
        var payableAccount = SumFor(settings.Payables);

        // PARITY-BUG #1: operator precedence is wrong here — division binds
        // tighter than subtraction so this evaluates to
        //   totalSale - ((operationalCosts + payableAccount) / totalSale)
        // The intended formula is (totalSale - costs) / totalSale.
        var profitMargin = totalSale == 0
            ? 0d
            : totalSale - ((double)(operationalCosts + payableAccount) / totalSale);

        var grossProfitLoss = SumFor(settings.ToCalculateGrossProfitLoss);
        var operationalProfitLoss = SumFor(settings.ToCalculateOperatingProfitLoss) + grossProfitLoss;
        // PARITY-BUG #2: adds OperationalCosts here instead of OperationalProfitLoss.
        var profitLossBeforTax = SumFor(settings.ToCalculateProfitLossBeforTax) + operationalCosts;
        var netProfitLoss = SumFor(settings.ToCalculateNetProfitLoss) + profitLossBeforTax;
        var accumulatedProfitLoss = SumFor(settings.ToCalculateAccumulatedProfitLoss) + netProfitLoss;

        var balanceItems = accounts
            .GroupBy(a => a.Type)
            .Select(g => new MainItem(
                g.Key,
                g.Select(a => new PanelDetailedItem(a.Name, a.Credit - a.Debit)).ToList()))
            .ToList();

        var allDetailValues = balanceItems
            .SelectMany(m => m.DetailedItems)
            .Where(d => d.Value.HasValue)
            .Select(d => d.Value!.Value)
            .ToList();

        // PARITY-BUG #3: both Assets and Liability filter Value>0 in the
        // legacy code — Liability is therefore always equal to Assets and
        // never reports negative balances as obligations.
        var assets = allDetailValues.Where(v => v > 0).Sum();
        var liability = allDetailValues.Where(v => v > 0).Sum();
        var equity = SumFor(settings.ToCalculateEquity);

        var stateCards = new FinancialStateCards(
            new Card<long>(totalSale, 0),
            new Card<double>(profitMargin, 0),
            new Card<long>(netProfitLoss, 0),
            new Card<long>(liquidity, 0));

        var balanceSheet = new FinancialBalanceSheet(
            new BalanceSheetStateCards(
                new Card<long>(assets, 0),
                new Card<long>(liability, 0),
                new Card<long>(equity, 0)),
            new BalanceSheetItems(balanceItems));

        var profitLoss = new FinancialProfitLossSheet(
            new ProfitLossItem(
                new PanelDetailedItem("GrossProfitLoss", grossProfitLoss),
                DetailsFor(settings.ToCalculateGrossProfitLoss)),
            new ProfitLossItem(
                new PanelDetailedItem("OperationalProfitLoss", operationalProfitLoss),
                DetailsFor(settings.ToCalculateOperatingProfitLoss)),
            new ProfitLossItem(
                new PanelDetailedItem("ProfitLossBeforTax", profitLossBeforTax),
                DetailsFor(settings.ToCalculateProfitLossBeforTax)),
            new ProfitLossItem(
                new PanelDetailedItem("NetProfitLoss", netProfitLoss),
                DetailsFor(settings.ToCalculateNetProfitLoss)),
            new ProfitLossItem(
                new PanelDetailedItem("AccumulatedProfitLoss", accumulatedProfitLoss),
                DetailsFor(settings.ToCalculateAccumulatedProfitLoss)));

        return new GetFinancialPanelResponse(stateCards, balanceSheet, profitLoss);
    }
}
