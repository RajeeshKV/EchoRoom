using System.Collections.Concurrent;

namespace Chat.Api.Services;

public class RecentMessageCache : IRecentMessageCache
{
    private const int MaxEntries = 2000;
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);
    private readonly ConcurrentDictionary<Guid, RecentMessageCacheItem> _items = new();
    private long _storeCount;

    public void Store(RecentMessageCacheItem item)
    {
        _items[item.Id] = item;
        var count = Interlocked.Increment(ref _storeCount);
        if (count % 100 == 0 || _items.Count > MaxEntries)
        {
            Trim();
        }
    }

    public bool TryGet(Guid messageId, out RecentMessageCacheItem? item)
    {
        if (_items.TryGetValue(messageId, out var found))
        {
            item = found;
            return true;
        }

        item = null;
        return false;
    }

    private void Trim()
    {
        var cutoff = DateTime.UtcNow.Subtract(MaxAge);
        foreach (var pair in _items)
        {
            if (pair.Value.SentAt < cutoff || _items.Count > MaxEntries)
            {
                _items.TryRemove(pair.Key, out _);
            }
        }
    }
}
