using Chat.Api.Data;
using Chat.Api.DTOs;
using Chat.Api.Entities;
using Chat.Api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Chat.Api.Services;

public class MessageService(AppDbContext dbContext, IUserConnectionService userConnectionService) : IMessageService
{
    public async Task<ChatMessageDto> SavePublicMessageAsync(string senderUsername, string message, CancellationToken cancellationToken)
    {
        var sanitized = MessageSanitizer.Sanitize(message);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new InvalidOperationException("Message content is empty after sanitization.");
        }

        var entity = new Message
        {
            SenderUsername = senderUsername,
            Content = sanitized,
            IsPrivate = false,
            RoomKey = "global",
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Messages.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ChatMessageDto
        {
            Sender = senderUsername,
            Message = entity.Content,
            SentAt = entity.CreatedAt
        };
    }

    public async Task<PrivateMessageDto> SavePrivateMessageAsync(string senderUsername, string receiverUsername, string message, CancellationToken cancellationToken)
    {
        var sanitized = MessageSanitizer.Sanitize(message);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new InvalidOperationException("Message content is empty after sanitization.");
        }

        var roomKey = userConnectionService.GetPrivateRoomKey(senderUsername, receiverUsername);
        var entity = new Message
        {
            SenderUsername = senderUsername,
            ReceiverUsername = receiverUsername,
            Content = sanitized,
            IsPrivate = true,
            RoomKey = roomKey,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Messages.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PrivateMessageDto
        {
            Sender = senderUsername,
            Receiver = receiverUsername,
            Message = entity.Content,
            SentAt = entity.CreatedAt,
            RoomKey = roomKey
        };
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
