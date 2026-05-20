using System.Threading.Channels;

namespace Chat.Api.Services;

public class MessagePersistenceQueue : IMessagePersistenceQueue
{
    private readonly Channel<MessagePersistenceItem> _channel = Channel.CreateUnbounded<MessagePersistenceItem>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask QueueAsync(MessagePersistenceItem item, CancellationToken cancellationToken)
        => _channel.Writer.WriteAsync(item, cancellationToken);

    public IAsyncEnumerable<MessagePersistenceItem> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
