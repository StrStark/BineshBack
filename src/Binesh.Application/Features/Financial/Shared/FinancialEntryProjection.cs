using System.Linq.Expressions;
using Binesh.Domain.Financial;

namespace Binesh.Application.Features.Financial.Shared;

internal static class FinancialEntryProjection
{
    public static readonly Expression<Func<FinancialEntry, FinancialEntryDto>> ToDto =
        e => new FinancialEntryDto(e.Id, e.Code, e.Name, e.Type, e.Debit, e.Credit);
}
