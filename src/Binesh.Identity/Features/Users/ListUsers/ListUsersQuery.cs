using Binesh.Identity.Features.Users.Shared;
using MediatR;

namespace Binesh.Identity.Features.Users.ListUsers;

public sealed record ListUsersQuery(string? Search, int Page, int PageSize)
    : IRequest<ListUsersResponse>;

public sealed record ListUsersResponse(
    IReadOnlyList<UserDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
