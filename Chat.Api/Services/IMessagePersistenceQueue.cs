namespace Chat.Api.Services;

public interface IMessagePersistenceQueue
{
    ValueTask QueueAsync(MessagePersistenceItem item, CancellationToken cancellationToken);
    IAsyncEnumerable<MessagePersistenceItem> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed record MessagePersistenceItem
{
    public Guid Id { get; init; }
    public string SenderUsername { get; init; } = string.Empty;
    public string? ReceiverUsername { get; init; }
    public string Content { get; init; } = string.Empty;
    public bool IsPrivate { get; init; }
    public string RoomKey { get; init; } = string.Empty;
    public Guid? ReplyToMessageId { get; init; }
    public string? AttachmentKind { get; init; }
    public string? AttachmentUrl { get; init; }
    public string? AttachmentFileName { get; init; }
    public string? AttachmentContentType { get; init; }
    public long? AttachmentSizeBytes { get; init; }
    public DateTime CreatedAt { get; init; }
}
