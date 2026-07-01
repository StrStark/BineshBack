using System.Linq.Expressions;
using Binesh.Domain.Sales;

namespace Binesh.Application.Features.SalesReturns.Shared;

internal static class SalesReturnProjection
{
    public static readonly Expression<Func<SalesReturn, SalesReturnDto>> ToDto =
        s => new SalesReturnDto(
            s.Id,
            s.Date,
            s.Price,
            s.Quantity,
            s.DeliveredQuantity,
            s.DocNumber,
            new SalesReturnProductRef(
                s.Product.Id,
                s.Product.ProductCode,
                s.Product.ProductDescription,
                s.Product.DetailedType),
            new SalesReturnCounterpartyRef(
                s.Counterparty.Id,
                s.Counterparty.Person.Name,
                s.Counterparty.Person.Family,
                s.Counterparty.Person.Mobile));
}
