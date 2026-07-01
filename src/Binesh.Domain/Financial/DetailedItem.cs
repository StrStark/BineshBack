namespace Binesh.Domain.Financial;

/// <summary>
/// Value object used inside <see cref="FinancialMappingSettings"/>. Each
/// mapping category is a list of these (the old code called them
/// <c>DetailedItem</c>; we keep the name for parity). In practice only
/// <see cref="Title"/> is consumed by the panel-aggregation handler — it
/// matches against <c>FinancialEntry.Name</c>. <see cref="Value"/> is
/// preserved as a nullable hint for future tooling.
/// </summary>
public sealed record DetailedItem(string Title, long? Value);
