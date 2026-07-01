namespace Binesh.Application.Features.Financial.Shared;

public sealed record FinancialEntryDto(
    Guid Id,
    string Code,
    string Name,
    string Type,
    long Debit,
    long Credit);
