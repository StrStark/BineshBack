using System.Text.Json.Serialization;

namespace Binesh.Domain.Chat;

/// <summary>
/// Author of a <see cref="ChatMessage"/>. <c>Tool</c> is reserved for a
/// future round where individual tool calls become first-class history rows
/// (so the UI can render them collapsibly); for Round 13a tool calls are
/// embedded as JSON inside the assistant message's content payload.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MessageRole>))]
public enum MessageRole
{
    User = 1,
    Assistant = 2,
    System = 3,
    Tool = 4,
}
