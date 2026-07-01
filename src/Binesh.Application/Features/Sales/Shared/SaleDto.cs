namespace Binesh.Application.Features.Sales.Shared;

/// <summary>
/// Read shape returned by every sales endpoint. Embeds the Product and
/// Counterparty summaries inline so clients don't need a second roundtrip.
/// </summary>
public sealed record SaleDto(
    Guid Id,
    DateTime Date,
    long Price,
    float Quantity,
    float DeliveredQuantity,
    int DocNumber,
    SaleProductRef Product,
    SaleCounterpartyRef Counterparty);

public sealed record SaleProductRef(
    Guid Id,
    string ProductCode,
    string ProductDescription,
    string DetailedType);

public sealed record SaleCounterpartyRef(
    Guid Id,
    string Name,
    string? Family,
    string? Mobile);
