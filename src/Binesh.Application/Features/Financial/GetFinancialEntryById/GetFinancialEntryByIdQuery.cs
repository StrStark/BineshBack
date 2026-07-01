using Binesh.Application.Features.Financial.Shared;
using MediatR;

namespace Binesh.Application.Features.Financial.GetFinancialEntryById;

public sealed record GetFinancialEntryByIdQuery(Guid Id) : IRequest<FinancialEntryDto>;
