using Chat.Api.Data;
using Chat.Api.Entities;
using Chat.Api.Services;

namespace Chat.Api.BackgroundServices;

public class MessagePersistenceService(
    IServiceScopeFactory serviceScopeFactory,
    IMessagePersistenceQueue messagePersistenceQueue,
    ILogger<MessagePersistenceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in messagePersistenceQueue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                dbContext.Messages.Add(new Message
                {
                    Id = item.Id,
                    SenderUsername = item.SenderUsername,
                    ReceiverUsername = item.ReceiverUsername,
                    Content = item.Content,
                    IsPrivate = item.IsPrivate,
                    RoomKey = item.RoomKey,
                    ReplyToMessageId = item.ReplyToMessageId,
                    AttachmentKind = item.AttachmentKind,
                    AttachmentUrl = item.AttachmentUrl,
                    AttachmentFileName = item.AttachmentFileName,
                    AttachmentContentType = item.AttachmentContentType,
                    AttachmentSizeBytes = item.AttachmentSizeBytes,
                    AttachmentStorageProvider = item.AttachmentStorageProvider,
                    AttachmentPublicId = item.AttachmentPublicId,
                    AttachmentResourceType = item.AttachmentResourceType,
                    CreatedAt = item.CreatedAt
                });

                await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Message persistence failed for room {RoomKey}.", item.RoomKey);
            }
        }
    }
}
