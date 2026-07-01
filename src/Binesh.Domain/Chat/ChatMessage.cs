namespace Binesh.Domain.Chat;

/// <summary>
/// One turn inside a <see cref="Conversation"/>. <see cref="Content"/> is an
/// opaque JSON payload stored as Postgres jsonb so the wire shape can evolve
/// (plain text today, structured tool-call audit + UI-component hints later)
/// without a migration per change. <see cref="Sequence"/> gives stable
/// ordering even when timestamps collide.
/// </summary>
public sealed class ChatMessage
{
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public int Sequence { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core
    private ChatMessage() { }

    public static ChatMessage Create(Guid conversationId, int sequence, MessageRole role, string contentJson)
    {
        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException("ConversationId is required.", nameof(conversationId));
        }
        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Sequence must be >= 1.");
        }
        if (string.IsNullOrWhiteSpace(contentJson))
        {
            throw new ArgumentException("Content cannot be blank.", nameof(contentJson));
        }

        return new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Sequence = sequence,
            Role = role,
            Content = contentJson,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
