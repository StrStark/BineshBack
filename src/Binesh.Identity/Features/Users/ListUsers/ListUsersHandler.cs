using Binesh.Application.Abstractions;
using Binesh.Identity.Features.Users.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Identity.Features.Users.ListUsers;

/// <summary>
/// Paginated user list. Old code fetched roles per row in a foreach (N+1).
/// Here roles come from a single join via UserRoles.
/// </summary>
public sealed class ListUsersHandler(IBineshDbContext db)
    : IRequestHandler<ListUsersQuery, ListUsersResponse>
{
    public async Task<ListUsersResponse> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                (u.FirstName != null && u.FirstName.ToLower().Contains(s)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(s)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(s)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageOfUsers = await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // One round-trip for roles instead of N — join UserRoles -> Roles.
        var userIds = pageOfUsers.Select(u => u.Id).ToList();
        var roleMap = await (
                from ur in db.UserRoles.AsNoTracking()
                where userIds.Contains(ur.UserId)
                join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                select new { ur.UserId, RoleName = r.Name! })
            .ToListAsync(cancellationToken);

        var rolesByUser = roleMap
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First().RoleName);

        var items = pageOfUsers
            .Select(u => new UserDto(
                u.Id,
                u.PhoneNumber ?? string.Empty,
                u.FirstName,
                u.LastName,
                u.JobTitle,
                u.BirthDate,
                u.ProfileImageName,
                rolesByUser.TryGetValue(u.Id, out var r) ? r : string.Empty,
                u.PhoneNumberConfirmed,
                u.CreatedAt))
            .ToList();

        return new ListUsersResponse(items, totalCount, request.Page, request.PageSize);
    }
}
