namespace Binesh.Domain.Support;

public sealed class SupportTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Subject { get; set; } = default!;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "open";
    public string Priority { get; set; } = "medium";
    public Guid AccountId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<SupportTicketMessage> Messages { get; set; } = [];
}
