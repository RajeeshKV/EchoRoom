using Chat.Api.Data;
using Chat.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Chat.Api.BackgroundServices;

public class MessageCleanupService(
    IServiceScopeFactory serviceScopeFactory,
    ICloudinaryMediaService cloudinaryMediaService,
    ILogger<MessageCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cutoff = DateTime.UtcNow.AddHours(-24);

                var expiredMessages = await dbContext.Messages
                    .Where(x => x.CreatedAt < cutoff)
                    .ToListAsync(stoppingToken);

                if (expiredMessages.Count > 0)
                {
                    var cloudinaryAssets = expiredMessages
                        .Where(x => string.Equals(x.AttachmentStorageProvider, "cloudinary", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(x.AttachmentPublicId)
                            && !string.IsNullOrWhiteSpace(x.AttachmentResourceType))
                        .Select(x => new { x.AttachmentPublicId, x.AttachmentResourceType })
                        .Distinct()
                        .ToList();

                    dbContext.Messages.RemoveRange(expiredMessages);
                    await dbContext.SaveChangesAsync(stoppingToken);

                    foreach (var asset in cloudinaryAssets)
                    {
                        try
                        {
                            var isStillReferenced = await dbContext.Messages.AnyAsync(
                                x => x.AttachmentStorageProvider == "cloudinary"
                                    && x.AttachmentPublicId == asset.AttachmentPublicId
                                    && x.AttachmentResourceType == asset.AttachmentResourceType,
                                stoppingToken);

                            if (isStillReferenced)
                            {
                                continue;
                            }

                            await cloudinaryMediaService.DeleteAsync(asset.AttachmentPublicId!, asset.AttachmentResourceType!, stoppingToken);
                        }
                        catch (Exception exception)
                        {
                            logger.LogWarning(
                                exception,
                                "Failed to delete Cloudinary asset {PublicId} during message cleanup.",
                                asset.AttachmentPublicId);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Message cleanup failed.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
