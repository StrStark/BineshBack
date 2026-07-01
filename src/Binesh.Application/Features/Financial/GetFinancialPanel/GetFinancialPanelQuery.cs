using MediatR;

namespace Binesh.Application.Features.Financial.GetFinancialPanel;

/// <summary>
/// Combined panel dashboard: state cards (totalSale, profitMargin, netProfit,
/// liquidity), balance sheet (assets / liabilities / equities + grouped items)
/// and profit-loss sheet (gross / operational / before-tax / net / accumulated).
///
/// <para>This port preserves the old <c>FinantialApiController.GetFinantialSummary</c>
/// math byte-for-byte even where that math is known to be wrong. See
/// <c>CHANGES.md</c> "Known parity issues" for the list — they are slated for
/// fix-up after the full transformation completes.</para>
/// </summary>
public sealed record GetFinancialPanelQuery : IRequest<GetFinancialPanelResponse>;

public sealed record GetFinancialPanelResponse(
    FinancialStateCards StateCards,
    FinancialBalanceSheet BalanceSheet,
    FinancialProfitLossSheet ProfitLoss);

public sealed record FinancialStateCards(
    Card<long> TotalSale,
    Card<double> ProfitMargin,
    Card<long> NetProfit,
    Card<long> Liquidity);

public sealed record Card<T>(T Value, double Growth);

public sealed record FinancialBalanceSheet(
    BalanceSheetStateCards StateCards,
    BalanceSheetItems Items);

public sealed record BalanceSheetStateCards(
    Card<long> Assets,
    Card<long> Liability,
    Card<long> Equities);

public sealed record BalanceSheetItems(IReadOnlyList<MainItem> MainItems);

public sealed record MainItem(string Title, IReadOnlyList<PanelDetailedItem> DetailedItems);

public sealed record PanelDetailedItem(string? Title, long? Value);

public sealed record FinancialProfitLossSheet(
    ProfitLossItem GrossProfitLoss,
    ProfitLossItem OperationalProfitLoss,
    ProfitLossItem ProfitLossBeforTax,
    ProfitLossItem NetProfitLoss,
    ProfitLossItem AccumulatedProfitLoss);

public sealed record ProfitLossItem(PanelDetailedItem Value, IReadOnlyList<PanelDetailedItem> Detailed);
