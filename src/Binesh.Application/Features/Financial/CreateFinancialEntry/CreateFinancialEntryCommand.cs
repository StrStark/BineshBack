using Binesh.Application.Features.Financial.Shared;
using MediatR;

namespace Binesh.Application.Features.Financial.CreateFinancialEntry;

public sealed record CreateFinancialEntryCommand(
    string Code,
    string Name,
    string Type,
    long Debit,
    long Credit)
    : IRequest<FinancialEntryDto>;
