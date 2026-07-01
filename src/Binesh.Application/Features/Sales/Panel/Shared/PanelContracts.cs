using Binesh.Domain.Customers;

namespace Binesh.Application.Features.Sales.Panel.Shared;

// ── Request contracts (ported 1:1 from the legacy panel DTOs) ────────────────
//
// The legacy endpoints took every filter through the POST body. These records
// mirror BineshSoloution.Dtos.Panel.Sales.SalesPageRequestDto and friends so the
// frontend payloads are unchanged.

/// <summary>Time bucket granularity for categorized grouping. Mirrors the legacy enum values.</summary>
public enum TimeFrameUnit
{
    Day = 1,
    Week = 2,
    Month = 3,
    Quarter = 4,
    Year = 5,
}

public sealed class DateFilterDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeFrameUnit TimeFrameUnit { get; set; }
}

public sealed class CategoryFilterDto
{
    public string? ProductCategory { get; set; }
}

public sealed class RequestProvince
{
    /// <summary>Legacy misspelling <c>Provinece</c> preserved for payload compatibility.</summary>
    public string? Provinece { get; set; }
}

public sealed class Paggination
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>Body for the summary / categorized / regional / top-selling endpoints.</summary>
public sealed class SalesPageRequestDto
{
    public DateFilterDto DateFilter { get; set; } = default!;
    public CategoryFilterDto CategoryDto { get; set; } = default!;
    public RequestProvince Provience { get; set; } = default!;
}

/// <summary>Body for the paginated records endpoint.</summary>
public sealed class SalesPageRequestPaginatedDto
{
    public DateFilterDto DateFilter { get; set; } = default!;
    public CategoryFilterDto CategoryDto { get; set; } = default!;
    public RequestProvince Provience { get; set; } = default!;
    public Paggination Paggination { get; set; } = default!;
    public string? SearchTerm { get; set; }
}

// ── Response contracts (ported 1:1) ──────────────────────────────────────────

public sealed class Card<T>
{
    public T Value { get; set; } = default!;
    public float Growth { get; set; }
}

public sealed class SoldItem
{
    public string? Type { get; set; }
    public long Value { get; set; }
    public float? Returned { get; set; }
}

public sealed class SalesCardsDto
{
    public Card<float> TotalSales { get; set; } = default!;
    public Card<float> ReturnTotal { get; set; } = default!;
    public Card<float> OffSales { get; set; } = default!;
    public Card<float> NewModelsSales { get; set; } = default!;
}

public sealed class SalesSummaryDto
{
    public List<SoldItem> SoldItems { get; set; } = default!;
    public int Count { get; set; }
    public long Sum { get; set; }
    public SalesCardsDto SalesCards { get; set; } = default!;
}

public sealed class CategorizedCustomer
{
    public CustomerType Type { get; set; }
    public int Count { get; set; }
    public DateTime OnDate { get; set; }
}

public sealed class CategorizedSales
{
    public List<CategorizedCustomer> Sales { get; set; } = default!;
}

public sealed class SaleOverRegionDto
{
    public string? City { get; set; }
    public long SalesPrice { get; set; }
    public float GrowthrRate { get; set; }
}

public sealed class RegionalSalesDto
{
    public List<SaleOverRegionDto> SaleOverRegion { get; set; } = default!;
    public long TotalSale { get; set; }
    public float GrowthrRate { get; set; }
}

public sealed class SalesRecordsDto
{
    public int FactorNume { get; set; }
    public string ProductDesc { get; set; } = default!;
    public string ProductCategory { get; set; } = default!;
    public float DeliverdQuantity { get; set; }
    public string CustomerName { get; set; } = default!;
    public long Price { get; set; }
    public DateTime Date { get; set; }
}

public sealed class TopProductItemDto
{
    public int Rank { get; set; }
    public string ProductName { get; set; } = default!;
    public int Count { get; set; }
    public float TotalAmount { get; set; }
    public float Growth { get; set; }
}

public sealed class TopSellingProductsDto
{
    public List<TopProductItemDto> Items { get; set; } = new();
}

/// <summary>Legacy paged-result envelope (with computed <see cref="TotalPages"/>).</summary>
public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public long TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
