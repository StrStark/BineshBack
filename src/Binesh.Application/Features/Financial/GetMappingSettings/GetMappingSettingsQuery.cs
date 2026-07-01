using Binesh.Application.Features.Financial.Shared;
using MediatR;

namespace Binesh.Application.Features.Financial.GetMappingSettings;

public sealed record GetMappingSettingsQuery : IRequest<MappingSettingsDto>;
