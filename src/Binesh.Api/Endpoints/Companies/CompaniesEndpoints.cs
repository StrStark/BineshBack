using System.Security.Claims;
using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Domain.Identity;
using Binesh.Domain.Tenancy;
using Binesh.Identity.Authorization;
using Binesh.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Api.Endpoints.Companies;

public static class CompaniesEndpoints
{
    public static IEndpointRouteBuilder MapCompaniesEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/companies", ListVisibleCompanies)
            .WithTags("Companies")
            .RequireAuthorization();

        var admin = routes.MapGroup("/api/admin/companies")
            .WithTags("AdminCompanies")
            .RequireAuthorization("role:Admin");

        admin.MapGet("/", ListVisibleCompanies).WithName(nameof(ListVisibleCompanies));
        admin.MapPost("/", CreateCompany).RequireAuthorization("role:SuperAdmin").WithName(nameof(CreateCompany));
        admin.MapGet("/{id:guid}", GetCompany).WithName(nameof(GetCompany));
        admin.MapPut("/{id:guid}", UpdateCompany).RequireAuthorization("role:SuperAdmin").WithName(nameof(UpdateCompany));
        admin.MapDelete("/{id:guid}", DeleteCompany).RequireAuthorization("role:SuperAdmin").WithName(nameof(DeleteCompany));
        admin.MapGet("/{id:guid}/users", ListCompanyUsers).WithName(nameof(ListCompanyUsers));
        admin.MapPost("/{id:guid}/users", CreateCompanyUser).RequireAuthorization("role:SuperAdmin").WithName(nameof(CreateCompanyUser));

        return routes;
    }

    private static async Task<IResult> ListVisibleCompanies(
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        var query = db.Companies.AsNoTracking();
        if (!user.IsInRole(AppRoles.SuperAdmin))
        {
            var companyId = await GetUserCompanyIdAsync(user, db, ct);
            query = query.Where(c => c.Id == companyId);
        }

        var companies = await query
            .OrderBy(c => c.Name)
            .Select(c => ToDto(c))
            .ToListAsync(ct);
        return Results.Ok(ApiEnvelope.Success(companies));
    }

    private static async Task<IResult> CreateCompany(
        [FromBody] CompanyRequest body,
        IBineshDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
        {
            throw new ConflictException("Company name is required.");
        }

        var slug = string.IsNullOrWhiteSpace(body.Slug) ? Slugify(body.Name) : Slugify(body.Slug);
        if (await db.Companies.AnyAsync(c => c.Slug == slug, ct))
        {
            throw new ConflictException($"Company slug '{slug}' already exists.", "company.duplicate_slug");
        }

        var company = new Company
        {
            Name = body.Name.Trim(),
            Slug = slug,
            Domain = NullIfBlank(body.Domain),
            Phone = NullIfBlank(body.Phone),
            Email = NullIfBlank(body.Email),
            Address = NullIfBlank(body.Address),
        };

        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/admin/companies/{company.Id}", ApiEnvelope.Success(ToDto(company)));
    }

    private static async Task<IResult> GetCompany(
        Guid id,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        await EnsureCanAccessCompanyAsync(id, user, db, ct);
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Company", id);
        return Results.Ok(ApiEnvelope.Success(ToDto(company)));
    }

    private static async Task<IResult> UpdateCompany(
        Guid id,
        [FromBody] CompanyRequest body,
        IBineshDbContext db,
        CancellationToken ct)
    {
        var company = await db.Companies.SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Company", id);
        if (!string.IsNullOrWhiteSpace(body.Name)) { company.Name = body.Name.Trim(); }
        if (!string.IsNullOrWhiteSpace(body.Slug))
        {
            var slug = Slugify(body.Slug);
            if (await db.Companies.AnyAsync(c => c.Id != id && c.Slug == slug, ct))
            {
                throw new ConflictException($"Company slug '{slug}' already exists.", "company.duplicate_slug");
            }
            company.Slug = slug;
        }
        company.Domain = NullIfBlank(body.Domain);
        company.Phone = NullIfBlank(body.Phone);
        company.Email = NullIfBlank(body.Email);
        company.Address = NullIfBlank(body.Address);
        company.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ApiEnvelope.Success(ToDto(company)));
    }

    private static async Task<IResult> DeleteCompany(Guid id, IBineshDbContext db, CancellationToken ct)
    {
        var company = await db.Companies.SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Company", id);
        var hasUsers = await db.Users.AnyAsync(u => u.CompanyId == id, ct);
        var hasDashboards = await db.Dashboards.AnyAsync(d => d.CompanyId == id, ct);
        var hasTickets = await db.SupportTickets.AnyAsync(t => t.CompanyId == id, ct);
        var hasCustomers = await db.Customers.AnyAsync(c => c.CompanyId == id, ct);
        var hasProducts = await db.Products.AnyAsync(p => p.CompanyId == id, ct);
        var hasSales = await db.Sales.AnyAsync(s => s.CompanyId == id, ct);
        var hasSalesReturns = await db.SalesReturns.AnyAsync(r => r.CompanyId == id, ct);
        var hasFinancialEntries = await db.FinancialEntries.AnyAsync(e => e.CompanyId == id, ct);
        var hasFinancialSettings = await db.FinancialMappingSettings.AnyAsync(s => s.CompanyId == id, ct);
        if (hasUsers
            || hasDashboards
            || hasTickets
            || hasCustomers
            || hasProducts
            || hasSales
            || hasSalesReturns
            || hasFinancialEntries
            || hasFinancialSettings)
        {
            throw new ConflictException(
                "Company cannot be deleted while it owns users, dashboards, tickets, or operational records.",
                "company.in_use");
        }

        db.Companies.Remove(company);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListCompanyUsers(
        Guid id,
        ClaimsPrincipal user,
        IBineshDbContext db,
        UserManager<User> userManager,
        CancellationToken ct)
    {
        await EnsureCanAccessCompanyAsync(id, user, db, ct);
        var users = await db.Users
            .AsNoTracking()
            .Where(u => u.CompanyId == id)
            .OrderBy(u => u.FirstName).ThenBy(u => u.PhoneNumber)
            .ToListAsync(ct);

        var result = new List<object>();
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            result.Add(new
            {
                id = u.Id,
                phoneNumber = u.PhoneNumber,
                firstName = u.FirstName,
                lastName = u.LastName,
                jobTitle = u.JobTitle,
                role = roles.FirstOrDefault() ?? AppRoles.User,
                companyId = u.CompanyId,
                createdAt = u.CreatedAt,
            });
        }
        return Results.Ok(ApiEnvelope.Success(result));
    }

    private static async Task<IResult> CreateCompanyUser(
        Guid id,
        [FromBody] CompanyUserRequest body,
        IBineshDbContext db,
        UserManager<User> userManager,
        CancellationToken ct)
    {
        if (!await db.Companies.AnyAsync(c => c.Id == id, ct))
        {
            throw new NotFoundException("Company", id);
        }

        var phone = PhoneNumberNormalizer.Normalize(body.PhoneNumber)
            ?? throw new ConflictException("Phone number is invalid.", "user.invalid_phone");
        if (await userManager.Users.AnyAsync(u => u.PhoneNumber == phone, ct))
        {
            throw new ConflictException($"A user with phone {phone} already exists.", "user.duplicate_phone");
        }

        var role = body.Role is AppRoles.Admin ? AppRoles.Admin : AppRoles.User;
        var user = new User
        {
            UserName = phone,
            PhoneNumber = phone,
            PhoneNumberConfirmed = false,
            FirstName = body.FirstName,
            LastName = body.LastName,
            JobTitle = body.JobTitle,
            CompanyId = id,
        };

        var create = await userManager.CreateAsync(user);
        if (!create.Succeeded)
        {
            throw new ConflictException(string.Join("; ", create.Errors.Select(e => e.Description)), "user.create_failed");
        }

        var addRole = await userManager.AddToRoleAsync(user, role);
        if (!addRole.Succeeded)
        {
            await userManager.DeleteAsync(user);
            throw new ConflictException(string.Join("; ", addRole.Errors.Select(e => e.Description)), "user.role_assignment_failed");
        }

        return Results.Created($"/api/users/{user.Id}", ApiEnvelope.Success(new
        {
            id = user.Id,
            phoneNumber = user.PhoneNumber,
            user.FirstName,
            user.LastName,
            user.JobTitle,
            role,
            companyId = user.CompanyId,
            user.CreatedAt,
        }));
    }

    private static async Task EnsureCanAccessCompanyAsync(
        Guid id,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        if (user.IsInRole(AppRoles.SuperAdmin)) { return; }
        var companyId = await GetUserCompanyIdAsync(user, db, ct);
        if (companyId != id) { throw new ForbiddenException(); }
    }

    private static async Task<Guid?> GetUserCompanyIdAsync(
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim."));
        return await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.CompanyId)
            .SingleAsync(ct);
    }

    private static object ToDto(Company c) => new
    {
        c.Id,
        c.Name,
        c.Slug,
        c.Domain,
        c.Logo,
        c.Phone,
        c.Email,
        c.Address,
        c.Status,
        c.CreatedAt,
        c.UpdatedAt,
    };

    private static string Slugify(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        return new string(chars).Trim('-');
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record CompanyRequest(
        string? Name,
        string? Slug,
        string? Domain,
        string? Phone,
        string? Email,
        string? Address);

    public sealed record CompanyUserRequest(
        string PhoneNumber,
        string? FirstName,
        string? LastName,
        string? JobTitle,
        string? Role);
}
