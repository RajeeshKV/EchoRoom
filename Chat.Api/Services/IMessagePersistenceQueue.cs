namespace Chat.Api.Services;

public interface IMessagePersistenceQueue
{
    ValueTask QueueAsync(MessagePersistenceItem item, CancellationToken cancellationToken);
    IAsyncEnumerable<MessagePersistenceItem> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed record MessagePersistenceItem
{
    public string SenderUsername { get; init; } = string.Empty;
    public string? ReceiverUsername { get; init; }
    public string Content { get; init; } = string.Empty;
    public bool IsPrivate { get; init; }
    public string RoomKey { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
