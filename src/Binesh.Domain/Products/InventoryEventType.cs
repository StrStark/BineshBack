using System.Text.Json.Serialization;

namespace Binesh.Domain.Products;

/// <summary>
/// Inventory ledger event types from the upstream ETL feed.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<InventoryEventType>))]
public enum InventoryEventType
{
    None = 0,

    /// <summary>رسید — stock came in (purchase received).</summary>
    Receipt = 1,

    /// <summary>حواله — stock went out (issued for sale / production).</summary>
    Issue = 2,

    /// <summary>درخواست فروش / مصرف.</summary>
    SalesOrConsumptionRequest = 3,

    /// <summary>درخواست خرید / تولید.</summary>
    PurchaseOrProductionRequest = 4,

    /// <summary>پیش‌فاکتور.</summary>
    ProformaInvoice = 5,

    /// <summary>فاکتور فروش.</summary>
    SalesInvoice = 6,
}
