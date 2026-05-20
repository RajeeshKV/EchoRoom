using Chat.Api.Data;
using Chat.Api.DTOs;
using Chat.Api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Chat.Api.Services;

public class MessageService(AppDbContext dbContext, IUserConnectionService userConnectionService) : IMessageService
{
    public ChatMessageEnvelope PreparePublicMessage(string senderUsername, string message)
    {
        var sanitized = MessageSanitizer.Sanitize(message);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new InvalidOperationException("Message content is empty after sanitization.");
        }

        var createdAt = DateTime.UtcNow;
        var persistenceItem = new MessagePersistenceItem
        {
            SenderUsername = senderUsername,
            Content = sanitized,
            IsPrivate = false,
            RoomKey = "global",
            CreatedAt = createdAt
        };

        return new ChatMessageEnvelope(
            new ChatMessageDto
            {
                Sender = senderUsername,
                Message = sanitized,
                SentAt = createdAt
            },
            persistenceItem);
    }

    public PrivateMessageEnvelope PreparePrivateMessage(string senderUsername, string receiverUsername, string message)
    {
        var sanitized = MessageSanitizer.Sanitize(message);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new InvalidOperationException("Message content is empty after sanitization.");
        }

        var roomKey = userConnectionService.GetPrivateRoomKey(senderUsername, receiverUsername);
        var createdAt = DateTime.UtcNow;
        var persistenceItem = new MessagePersistenceItem
        {
            SenderUsername = senderUsername,
            ReceiverUsername = receiverUsername,
            Content = sanitized,
            IsPrivate = true,
            RoomKey = roomKey,
            CreatedAt = createdAt
        };

        return new PrivateMessageEnvelope(
            new PrivateMessageDto
            {
                Sender = senderUsername,
                Receiver = receiverUsername,
                Message = sanitized,
                SentAt = createdAt,
                RoomKey = roomKey
            },
            persistenceItem);
    }

    public async Task<IReadOnlyCollection<ChatMessageDto>> GetPublicMessagesAsync(int take, CancellationToken cancellationToken)
    {
        var safeTake = Math.Clamp(take, 1, 100);

        return await dbContext.Messages
            .Where(x => !x.IsPrivate && x.RoomKey == "global")
            .OrderByDescending(x => x.CreatedAt)
            .Take(safeTake)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new ChatMessageDto
            {
                Sender = x.SenderUsername,
                Message = x.Content,
                SentAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PrivateMessageDto>> GetPrivateMessagesAsync(string firstUsername, string secondUsername, int take, CancellationToken cancellationToken)
    {
        var safeTake = Math.Clamp(take, 1, 100);
        var roomKey = userConnectionService.GetPrivateRoomKey(firstUsername, secondUsername);

        return await dbContext.Messages
            .Where(x => x.IsPrivate && x.RoomKey == roomKey)
            .OrderByDescending(x => x.CreatedAt)
            .Take(safeTake)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new PrivateMessageDto
            {
                Sender = x.SenderUsername,
                Receiver = x.ReceiverUsername ?? string.Empty,
                Message = x.Content,
                SentAt = x.CreatedAt,
                RoomKey = x.RoomKey
            })
            .ToListAsync(cancellationToken);
    }
}
