using Chat.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Chat.Api.BackgroundServices;

public class MessageCleanupService(IServiceScopeFactory serviceScopeFactory, ILogger<MessageCleanupService> logger) : BackgroundService
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
                    dbContext.Messages.RemoveRange(expiredMessages);
                    await dbContext.SaveChangesAsync(stoppingToken);
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
