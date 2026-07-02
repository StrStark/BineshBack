namespace Binesh.Domain.Support;

public sealed class SupportTicketMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public string Text { get; set; } = default!;
    public string Sender { get; set; } = "user";
    public Guid? AccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
