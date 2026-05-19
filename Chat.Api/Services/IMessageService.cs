using Chat.Api.DTOs;

namespace Chat.Api.Services;

public interface IMessageService
{
    Task<ChatMessageDto> SavePublicMessageAsync(string senderUsername, string message, CancellationToken cancellationToken);
    Task<PrivateMessageDto> SavePrivateMessageAsync(string senderUsername, string receiverUsername, string message, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ChatMessageDto>> GetPublicMessagesAsync(int take, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PrivateMessageDto>> GetPrivateMessagesAsync(string firstUsername, string secondUsername, int take, CancellationToken cancellationToken);
}
