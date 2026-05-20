using Chat.Api.DTOs;

namespace Chat.Api.Services;

public interface IMessageService
{
    ChatMessageEnvelope PreparePublicMessage(string senderUsername, string message);
    PrivateMessageEnvelope PreparePrivateMessage(string senderUsername, string receiverUsername, string message);
    Task<IReadOnlyCollection<ChatMessageDto>> GetPublicMessagesAsync(int take, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PrivateMessageDto>> GetPrivateMessagesAsync(string firstUsername, string secondUsername, int take, CancellationToken cancellationToken);
}

public sealed record ChatMessageEnvelope(ChatMessageDto Message, MessagePersistenceItem PersistenceItem);

public sealed record PrivateMessageEnvelope(PrivateMessageDto Message, MessagePersistenceItem PersistenceItem);
