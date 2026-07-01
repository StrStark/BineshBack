namespace Binesh.Application.Features.Chat.Shared;

public sealed record ConversationDto(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt,
    int MessageCount);

public sealed record ConversationWithMessagesDto(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<ChatMessageDto> Messages);

public sealed record ChatMessageDto(
    Guid Id,
    int Sequence,
    string Role,
    string Content,
    DateTimeOffset CreatedAt);
