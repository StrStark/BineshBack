using System.Linq.Expressions;
using Binesh.Domain.Sales;

namespace Binesh.Application.Features.Sales.Shared;

/// <summary>
/// Single source of truth for Sale → SaleDto projection. Used by GetSaleById,
/// ListSales, CreateSale, and UpdateSale so every endpoint returns identical
/// shape. Pure expression — translates to SQL.
/// </summary>
internal static class SaleProjection
{
    public static readonly Expression<Func<Sale, SaleDto>> ToDto =
        s => new SaleDto(
            s.Id,
            s.Date,
            s.Price,
            s.Quantity,
            s.DeliveredQuantity,
            s.DocNumber,
            new SaleProductRef(
                s.Product.Id,
                s.Product.ProductCode,
                s.Product.ProductDescription,
                s.Product.DetailedType),
            new SaleCounterpartyRef(
                s.Counterparty.Id,
                s.Counterparty.Person.Name,
                s.Counterparty.Person.Family,
                s.Counterparty.Person.Mobile));
}
