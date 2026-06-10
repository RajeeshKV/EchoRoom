using Chat.Api.DTOs;

namespace Chat.Api.Services;

public interface IRecentMessageCache
{
    void Store(RecentMessageCacheItem item);
    bool TryGet(Guid messageId, out RecentMessageCacheItem? item);
}

public sealed record RecentMessageCacheItem(
    Guid Id,
    string RoomKey,
    string Sender,
    string Message,
    string? AttachmentKind,
    DateTime SentAt);
