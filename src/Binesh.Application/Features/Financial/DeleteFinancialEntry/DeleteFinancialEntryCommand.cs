using MediatR;

namespace Binesh.Application.Features.Financial.DeleteFinancialEntry;

public sealed record DeleteFinancialEntryCommand(Guid Id) : IRequest;
