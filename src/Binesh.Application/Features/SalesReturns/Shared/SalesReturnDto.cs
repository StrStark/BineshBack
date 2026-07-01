namespace Binesh.Application.Features.SalesReturns.Shared;

public sealed record SalesReturnDto(
    Guid Id,
    DateTime Date,
    long Price,
    float Quantity,
    float DeliveredQuantity,
    int DocNumber,
    SalesReturnProductRef Product,
    SalesReturnCounterpartyRef Counterparty);

public sealed record SalesReturnProductRef(
    Guid Id,
    string ProductCode,
    string ProductDescription,
    string DetailedType);

public sealed record SalesReturnCounterpartyRef(
    Guid Id,
    string Name,
    string? Family,
    string? Mobile);
