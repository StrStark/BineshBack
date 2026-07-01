using Binesh.Application.Features.Financial.Shared;
using MediatR;

namespace Binesh.Application.Features.Financial.UpdateFinancialEntry;

public sealed record UpdateFinancialEntryCommand(
    Guid Id,
    string? Code,
    string? Name,
    string? Type,
    long? Debit,
    long? Credit)
    : IRequest<FinancialEntryDto>;
