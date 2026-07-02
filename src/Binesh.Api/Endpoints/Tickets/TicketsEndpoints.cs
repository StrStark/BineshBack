using System.Security.Claims;
using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Domain.Support;
using Binesh.Identity.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Api.Endpoints.Tickets;

public static class TicketsEndpoints
{
    public static IEndpointRouteBuilder MapTicketsEndpoints(this IEndpointRouteBuilder routes)
    {
        var userGroup = routes.MapGroup("/api/tickets")
            .WithTags("Tickets")
            .RequireAuthorization();
        userGroup.MapGet("/", ListMyTickets).WithName(nameof(ListMyTickets));
        userGroup.MapPost("/", CreateTicket).WithName(nameof(CreateTicket));
        userGroup.MapGet("/{id:guid}", GetMyTicket).WithName(nameof(GetMyTicket));
        userGroup.MapPost("/{id:guid}/reply", ReplyToTicket).WithName(nameof(ReplyToTicket));

        var adminGroup = routes.MapGroup("/api/admin/tickets")
            .WithTags("AdminTickets")
            .RequireAuthorization("role:Admin");
        adminGroup.MapGet("/", ListAdminTickets).WithName(nameof(ListAdminTickets));
        adminGroup.MapPost("/", CreateTicket).WithName("AdminCreateTicket");
        adminGroup.MapGet("/{id:guid}", GetAdminTicket).WithName(nameof(GetAdminTicket));
        adminGroup.MapPatch("/{id:guid}", PatchTicket).WithName(nameof(PatchTicket));
        adminGroup.MapDelete("/{id:guid}", DeleteTicket).WithName(nameof(DeleteTicket));
        adminGroup.MapPost("/{id:guid}/reply", AdminReplyToTicket).WithName(nameof(AdminReplyToTicket));

        return routes;
    }

    private static async Task<IResult> ListMyTickets(
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        var userId = RequireUserId(user);
        var tickets = await db.SupportTickets.AsNoTracking()
            .Where(t => t.AccountId == userId)
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => ToListDto(t))
            .ToListAsync(ct);
        return Results.Ok(ApiEnvelope.Success(tickets));
    }

    private static async Task<IResult> ListAdminTickets(
        string? status,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        var companyId = await GetUserCompanyIdAsync(user, db, ct);
        var query = db.SupportTickets.AsNoTracking();
        if (!user.IsInRole(AppRoles.SuperAdmin))
        {
            query = query.Where(t => t.CompanyId == companyId);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        var tickets = await query
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => ToListDto(t))
            .ToListAsync(ct);
        return Results.Ok(ApiEnvelope.Success(tickets));
    }

    private static async Task<IResult> CreateTicket(
        [FromBody] TicketRequest body,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Subject))
        {
            throw new ConflictException("Ticket subject is required.");
        }

        var userId = RequireUserId(user);
        var userCompanyId = await GetUserCompanyIdAsync(user, db, ct);
        var companyId = body.CompanyId ?? userCompanyId
            ?? throw new ConflictException("A company is required before support tickets can be created.", "ticket.company_required");
        if (!user.IsInRole(AppRoles.SuperAdmin) && body.CompanyId is not null && body.CompanyId != userCompanyId)
        {
            throw new ForbiddenException();
        }

        var ticket = new SupportTicket
        {
            Subject = body.Subject.Trim(),
            Description = body.Description?.Trim() ?? string.Empty,
            Priority = string.IsNullOrWhiteSpace(body.Priority) ? "medium" : body.Priority.Trim(),
            Status = "open",
            AccountId = body.AccountId ?? userId,
            CompanyId = companyId,
        };
        if (!string.IsNullOrWhiteSpace(ticket.Description))
        {
            ticket.Messages.Add(new SupportTicketMessage
            {
                Text = ticket.Description,
                Sender = "user",
                AccountId = ticket.AccountId,
            });
        }

        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/tickets/{ticket.Id}", ApiEnvelope.Success(await ToDetailsAsync(ticket.Id, db, ct)));
    }

    private static async Task<IResult> GetMyTicket(
        Guid id,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        var ticket = await LoadTicketAsync(id, db, ct);
        if (ticket.AccountId != RequireUserId(user)) { throw new ForbiddenException(); }
        return Results.Ok(ApiEnvelope.Success(await ToDetailsAsync(id, db, ct)));
    }

    private static async Task<IResult> GetAdminTicket(
        Guid id,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        await EnsureAdminCanAccessTicketAsync(id, user, db, ct);
        return Results.Ok(ApiEnvelope.Success(await ToDetailsAsync(id, db, ct)));
    }

    private static Task<IResult> ReplyToTicket(
        Guid id,
        [FromBody] TicketReplyRequest body,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct) =>
        AddReplyAsync(id, body, user, db, sender: "user", requireOwner: true, ct);

    private static Task<IResult> AdminReplyToTicket(
        Guid id,
        [FromBody] TicketReplyRequest body,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct) =>
        AddReplyAsync(id, body, user, db, sender: "admin", requireOwner: false, ct);

    private static async Task<IResult> AddReplyAsync(
        Guid id,
        TicketReplyRequest body,
        ClaimsPrincipal user,
        IBineshDbContext db,
        string sender,
        bool requireOwner,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Text))
        {
            throw new ConflictException("Reply text is required.");
        }

        var ticket = await LoadTicketAsync(id, db, ct, tracking: true);
        var userId = RequireUserId(user);
        if (requireOwner && ticket.AccountId != userId) { throw new ForbiddenException(); }
        if (!requireOwner) { await EnsureAdminCanAccessTicketAsync(id, user, db, ct); }

        ticket.Messages.Add(new SupportTicketMessage
        {
            Text = body.Text.Trim(),
            Sender = sender,
            AccountId = userId,
        });
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        if (sender == "admin" && ticket.Status == "open") { ticket.Status = "pending"; }
        await db.SaveChangesAsync(ct);
        return Results.Ok(ApiEnvelope.Success(await ToDetailsAsync(id, db, ct)));
    }

    private static async Task<IResult> PatchTicket(
        Guid id,
        [FromBody] TicketPatchRequest body,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        await EnsureAdminCanAccessTicketAsync(id, user, db, ct);
        var ticket = await LoadTicketAsync(id, db, ct, tracking: true);
        if (!string.IsNullOrWhiteSpace(body.Status)) { ticket.Status = body.Status.Trim(); }
        if (!string.IsNullOrWhiteSpace(body.Priority)) { ticket.Priority = body.Priority.Trim(); }
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ApiEnvelope.Success(await ToDetailsAsync(id, db, ct)));
    }

    private static async Task<IResult> DeleteTicket(
        Guid id,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        await EnsureAdminCanAccessTicketAsync(id, user, db, ct);
        var ticket = await LoadTicketAsync(id, db, ct, tracking: true);
        db.SupportTickets.Remove(ticket);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task EnsureAdminCanAccessTicketAsync(
        Guid id,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        var ticketCompanyId = await db.SupportTickets.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => t.CompanyId)
            .Cast<Guid?>()
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("SupportTicket", id);
        if (user.IsInRole(AppRoles.SuperAdmin)) { return; }
        var companyId = await GetUserCompanyIdAsync(user, db, ct);
        if (ticketCompanyId != companyId) { throw new ForbiddenException(); }
    }

    private static async Task<SupportTicket> LoadTicketAsync(
        Guid id,
        IBineshDbContext db,
        CancellationToken ct,
        bool tracking = false)
    {
        var query = tracking ? db.SupportTickets.Include(t => t.Messages) : db.SupportTickets.AsNoTracking().Include(t => t.Messages);
        return await query.SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("SupportTicket", id);
    }

    private static async Task<object> ToDetailsAsync(Guid id, IBineshDbContext db, CancellationToken ct)
    {
        var ticket = await db.SupportTickets.AsNoTracking()
            .Include(t => t.Messages)
            .SingleAsync(t => t.Id == id, ct);
        return new
        {
            ticket.Id,
            ticket.Subject,
            ticket.Description,
            ticket.Status,
            ticket.Priority,
            ticket.AccountId,
            ticket.CompanyId,
            ticket.CreatedAt,
            ticket.UpdatedAt,
            messages = ticket.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(m => new { m.Id, m.Text, m.Sender, m.AccountId, m.CreatedAt })
                .ToList(),
        };
    }

    private static object ToListDto(SupportTicket ticket) => new
    {
        ticket.Id,
        ticket.Subject,
        ticket.Description,
        ticket.Status,
        ticket.Priority,
        ticket.AccountId,
        ticket.CompanyId,
        ticket.CreatedAt,
        ticket.UpdatedAt,
    };

    private static async Task<Guid?> GetUserCompanyIdAsync(
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        var userId = RequireUserId(user);
        return await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.CompanyId)
            .SingleAsync(ct);
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim."));

    public sealed record TicketRequest(
        string Subject,
        string? Description,
        string? Priority,
        Guid? CompanyId,
        Guid? AccountId);

    public sealed record TicketReplyRequest(string Text);

    public sealed record TicketPatchRequest(string? Status, string? Priority);
}
