namespace Binesh.Application.Features.Customers.Shared;

/// <summary>
/// Wire shape for a customer's contact details. <c>Mobile</c> is what the old
/// schema called <c>PhoneNumber</c> on Person — renamed because the User entity
/// also has a <c>PhoneNumber</c> (login phone) and the collision caused confusion.
/// </summary>
public sealed record PersonDto(
    Guid? Id,
    string Name,
    string? Family,
    string? Code,
    string? Phone,
    string? Mobile,
    string? Fax,
    string? Pelak,
    string? Address,
    DateTimeOffset? BirthDate,
    RegionDto? Region);
