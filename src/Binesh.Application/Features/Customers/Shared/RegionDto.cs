namespace Binesh.Application.Features.Customers.Shared;

public sealed record RegionDto(
    Guid? Id,
    string? Country,
    string? Province,
    string? City);
