using Chat.Api.Data;
using Chat.Api.DTOs;
using Chat.Api.Entities;
using Chat.Api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Chat.Api.Services;

public class MessageService(
    AppDbContext dbContext,
    IUserConnectionService userConnectionService,
    IChatMediaService chatMediaService,
    IRecentMessageCache recentMessageCache) : IMessageService
{
    public async Task<ChatMessageEnvelope> PreparePublicMessageAsync(string senderUsername, SendChatMessageRequest request, CancellationToken cancellationToken)
    {
        var roomKey = userConnectionService.GlobalRoomName;
        var content = NormalizeContent(request.Message);
        var attachment = await NormalizeAttachmentAsync(request.Attachment, cancellationToken);
        EnsurePayloadExists(content, attachment);
        var replyTo = await BuildReplyPreviewAsync(roomKey, request.ReplyToMessageId, cancellationToken);

        return BuildPublicEnvelope(senderUsername, content, roomKey, attachment, replyTo);
    }

    public async Task<PrivateMessageEnvelope> PreparePrivateMessageAsync(string senderUsername, string receiverUsername, SendChatMessageRequest request, CancellationToken cancellationToken)
    {
        var roomKey = userConnectionService.GetPrivateRoomKey(senderUsername, receiverUsername);
        var content = NormalizeContent(request.Message);
        var attachment = await NormalizeAttachmentAsync(request.Attachment, cancellationToken);
        EnsurePayloadExists(content, attachment);
        var replyTo = await BuildReplyPreviewAsync(roomKey, request.ReplyToMessageId, cancellationToken);

        var messageId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        recentMessageCache.Store(new RecentMessageCacheItem(
            messageId,
            roomKey,
            senderUsername,
            content,
            attachment?.Kind,
            createdAt));

        var message = new PrivateMessageDto
        {
            Id = messageId,
            Sender = senderUsername,
            Receiver = receiverUsername,
            Message = content,
            Attachment = attachment,
            ReplyTo = replyTo,
            SentAt = createdAt,
            RoomKey = roomKey
        };

        var persistenceItem = new MessagePersistenceItem
        {
            Id = messageId,
            SenderUsername = senderUsername,
            ReceiverUsername = receiverUsername,
            Content = content,
            IsPrivate = true,
            RoomKey = roomKey,
            ReplyToMessageId = replyTo?.MessageId,
            AttachmentKind = attachment?.Kind,
            AttachmentUrl = attachment?.Url,
            AttachmentFileName = attachment?.FileName,
            AttachmentContentType = attachment?.ContentType,
            AttachmentSizeBytes = attachment?.SizeBytes,
            AttachmentStorageProvider = attachment?.Provider,
            AttachmentPublicId = attachment?.PublicId,
            AttachmentResourceType = attachment?.ResourceType,
            CreatedAt = createdAt
        };

        return new PrivateMessageEnvelope(message, persistenceItem);
    }

    public async Task<IReadOnlyCollection<ChatMessageDto>> GetPublicMessagesAsync(int take, CancellationToken cancellationToken)
    {
        var safeTake = Math.Clamp(take, 1, 100);
        var messages = await dbContext.Messages
            .Where(x => !x.IsPrivate && x.RoomKey == userConnectionService.GlobalRoomName)
            .OrderByDescending(x => x.CreatedAt)
            .Take(safeTake)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return await MapPublicMessagesAsync(messages, cancellationToken);
    }

    public async Task<IReadOnlyCollection<PrivateMessageDto>> GetPrivateMessagesAsync(string firstUsername, string secondUsername, int take, CancellationToken cancellationToken)
    {
        var safeTake = Math.Clamp(take, 1, 100);
        var roomKey = userConnectionService.GetPrivateRoomKey(firstUsername, secondUsername);
        var messages = await dbContext.Messages
            .Where(x => x.IsPrivate && x.RoomKey == roomKey)
            .OrderByDescending(x => x.CreatedAt)
            .Take(safeTake)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return await MapPrivateMessagesAsync(messages, cancellationToken);
    }

    private ChatMessageEnvelope BuildPublicEnvelope(
        string senderUsername,
        string content,
        string roomKey,
        ChatAttachmentDto? attachment,
        ReplyMessageDto? replyTo)
    {
        var messageId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        recentMessageCache.Store(new RecentMessageCacheItem(
            messageId,
            roomKey,
            senderUsername,
            content,
            attachment?.Kind,
            createdAt));

        var message = new ChatMessageDto
        {
            Id = messageId,
            Sender = senderUsername,
            Message = content,
            Attachment = attachment,
            ReplyTo = replyTo,
            SentAt = createdAt
        };

        var persistenceItem = new MessagePersistenceItem
        {
            Id = messageId,
            SenderUsername = senderUsername,
            Content = content,
            IsPrivate = false,
            RoomKey = roomKey,
            ReplyToMessageId = replyTo?.MessageId,
            AttachmentKind = attachment?.Kind,
            AttachmentUrl = attachment?.Url,
            AttachmentFileName = attachment?.FileName,
            AttachmentContentType = attachment?.ContentType,
            AttachmentSizeBytes = attachment?.SizeBytes,
            AttachmentStorageProvider = attachment?.Provider,
            AttachmentPublicId = attachment?.PublicId,
            AttachmentResourceType = attachment?.ResourceType,
            CreatedAt = createdAt
        };

        return new ChatMessageEnvelope(message, persistenceItem);
    }

    private async Task<ReplyMessageDto?> BuildReplyPreviewAsync(string roomKey, Guid? replyToMessageId, CancellationToken cancellationToken)
    {
        if (replyToMessageId is null)
        {
            return null;
        }

        if (recentMessageCache.TryGet(replyToMessageId.Value, out var cachedItem) && cachedItem is not null)
        {
            if (!string.Equals(cachedItem.RoomKey, roomKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Reply target is not in the same room.");
            }

            return new ReplyMessageDto
            {
                MessageId = cachedItem.Id,
                Sender = cachedItem.Sender,
                Message = BuildReplyPreviewText(cachedItem.Message, cachedItem.AttachmentKind),
                AttachmentKind = cachedItem.AttachmentKind
            };
        }

        var replyTarget = await dbContext.Messages
            .Where(x => x.Id == replyToMessageId.Value)
            .Select(x => new
            {
                x.Id,
                x.RoomKey,
                x.SenderUsername,
                x.Content,
                x.AttachmentKind
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (replyTarget is null)
        {
            throw new InvalidOperationException("Reply target was not found.");
        }

        if (!string.Equals(replyTarget.RoomKey, roomKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Reply target is not in the same room.");
        }

        return new ReplyMessageDto
        {
            MessageId = replyTarget.Id,
            Sender = replyTarget.SenderUsername,
            Message = BuildReplyPreviewText(replyTarget.Content, replyTarget.AttachmentKind),
            AttachmentKind = replyTarget.AttachmentKind
        };
    }

    private async Task<Dictionary<Guid, ReplyMessageDto>> LoadReplyLookupAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
    {
        var replyIds = messages
            .Where(x => x.ReplyToMessageId.HasValue)
            .Select(x => x.ReplyToMessageId!.Value)
            .Distinct()
            .ToArray();

        if (replyIds.Length == 0)
        {
            return [];
        }

        var replyTargets = await dbContext.Messages
            .Where(x => replyIds.Contains(x.Id))
            .Select(x => new ReplyMessageDto
            {
                MessageId = x.Id,
                Sender = x.SenderUsername,
                Message = BuildReplyPreviewText(x.Content, x.AttachmentKind),
                AttachmentKind = x.AttachmentKind
            })
            .ToListAsync(cancellationToken);

        return replyTargets.ToDictionary(x => x.MessageId);
    }

    private async Task<IReadOnlyCollection<ChatMessageDto>> MapPublicMessagesAsync(List<Message> messages, CancellationToken cancellationToken)
    {
        var replyLookup = await LoadReplyLookupAsync(messages, cancellationToken);
        return messages.Select(x => new ChatMessageDto
        {
            Id = x.Id,
            Sender = x.SenderUsername,
            Message = x.Content,
            Attachment = BuildAttachment(x),
            ReplyTo = x.ReplyToMessageId.HasValue && replyLookup.TryGetValue(x.ReplyToMessageId.Value, out var reply) ? reply : null,
            SentAt = x.CreatedAt
        }).ToList();
    }

    private async Task<IReadOnlyCollection<PrivateMessageDto>> MapPrivateMessagesAsync(List<Message> messages, CancellationToken cancellationToken)
    {
        var replyLookup = await LoadReplyLookupAsync(messages, cancellationToken);
        return messages.Select(x => new PrivateMessageDto
        {
            Id = x.Id,
            Sender = x.SenderUsername,
            Receiver = x.ReceiverUsername ?? string.Empty,
            Message = x.Content,
            Attachment = BuildAttachment(x),
            ReplyTo = x.ReplyToMessageId.HasValue && replyLookup.TryGetValue(x.ReplyToMessageId.Value, out var reply) ? reply : null,
            SentAt = x.CreatedAt,
            RoomKey = x.RoomKey
        }).ToList();
    }

    private static ChatAttachmentDto? BuildAttachment(Message message)
    {
        if (string.IsNullOrWhiteSpace(message.AttachmentKind) || string.IsNullOrWhiteSpace(message.AttachmentUrl))
        {
            return null;
        }

        return new ChatAttachmentDto
        {
            Kind = message.AttachmentKind,
            Url = message.AttachmentUrl,
            FileName = message.AttachmentFileName ?? string.Empty,
            ContentType = message.AttachmentContentType ?? string.Empty,
            SizeBytes = message.AttachmentSizeBytes ?? 0,
            Provider = message.AttachmentStorageProvider ?? string.Empty,
            PublicId = message.AttachmentPublicId ?? string.Empty,
            ResourceType = message.AttachmentResourceType ?? string.Empty
        };
    }

    private async Task<ChatAttachmentDto?> NormalizeAttachmentAsync(ChatAttachmentDto? attachment, CancellationToken cancellationToken)
    {
        if (attachment is null)
        {
            return null;
        }

        return await chatMediaService.ValidateAsync(attachment, cancellationToken);
    }

    private static string NormalizeContent(string? content)
    {
        var sanitized = MessageSanitizer.Sanitize(content ?? string.Empty);
        return sanitized;
    }

    private static void EnsurePayloadExists(string content, ChatAttachmentDto? attachment)
    {
        if (string.IsNullOrWhiteSpace(content) && attachment is null)
        {
            throw new InvalidOperationException("A message must include text or an attachment.");
        }
    }

    private static string BuildReplyPreviewText(string content, string? attachmentKind)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            return content.Length <= 120 ? content : $"{content[..120]}...";
        }

        return attachmentKind switch
        {
            ChatAttachmentRules.ImageKind => "[image]",
            ChatAttachmentRules.VoiceKind => "[voice]",
            ChatAttachmentRules.VideoKind => "[video]",
            _ => "[attachment]"
        };
    }
}
