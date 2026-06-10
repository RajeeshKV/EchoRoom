using Chat.Api.DTOs;

namespace Chat.Api.Services;

public interface IMessageService
{
    Task<ChatMessageEnvelope> PreparePublicMessageAsync(string senderUsername, SendChatMessageRequest request, CancellationToken cancellationToken);
    Task<PrivateMessageEnvelope> PreparePrivateMessageAsync(string senderUsername, string receiverUsername, SendChatMessageRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ChatMessageDto>> GetPublicMessagesAsync(int take, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PrivateMessageDto>> GetPrivateMessagesAsync(string firstUsername, string secondUsername, int take, CancellationToken cancellationToken);
}

public sealed record ChatMessageEnvelope(ChatMessageDto Message, MessagePersistenceItem PersistenceItem);

public sealed record PrivateMessageEnvelope(PrivateMessageDto Message, MessagePersistenceItem PersistenceItem);
