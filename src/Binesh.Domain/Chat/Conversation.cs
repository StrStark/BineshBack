namespace Binesh.Domain.Chat;

/// <summary>
/// A multi-turn AI chat session owned by exactly one user. Soft-deleted via
/// <see cref="ArchivedAt"/> so the row stays available for audit / abuse
/// reporting after the user removes it from their UI.
/// </summary>
public sealed class Conversation
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    private readonly List<ChatMessage> _messages = [];
    public IReadOnlyList<ChatMessage> Messages => _messages;

    // EF Core
    private Conversation() { }

    public static Conversation Start(Guid userId, string title)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        return new Conversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be blank.", nameof(title));
        }
        Title = title.Trim();
    }

    public void Archive()
    {
        ArchivedAt ??= DateTimeOffset.UtcNow;
    }

    public void Unarchive()
    {
        ArchivedAt = null;
    }

    public ChatMessage AppendMessage(MessageRole role, string contentJson)
    {
        var nextSequence = _messages.Count == 0 ? 1 : _messages[^1].Sequence + 1;
        var message = ChatMessage.Create(Id, nextSequence, role, contentJson);
        _messages.Add(message);
        return message;
    }
}
