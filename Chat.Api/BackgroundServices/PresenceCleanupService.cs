using Chat.Api.Hubs;
using Chat.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace Chat.Api.BackgroundServices;

public class PresenceCleanupService(
    IServiceScopeFactory serviceScopeFactory,
    IHubContext<ChatHub> hubContext,
    ILogger<PresenceCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var userConnectionService = scope.ServiceProvider.GetRequiredService<IUserConnectionService>();
                var staleUsers = await userConnectionService.MarkStaleUsersOfflineAsync(StaleAfter, stoppingToken);

                if (staleUsers.Count > 0)
                {
                    foreach (var username in staleUsers)
                    {
                        await hubContext.Clients.Group(userConnectionService.GlobalRoomName)
                            .SendAsync("UserLeft", username, stoppingToken);
                    }

                    var activeUsers = await userConnectionService.GetActiveUsersAsync(stoppingToken);
                    await hubContext.Clients.All.SendAsync("ActiveUsersUpdated", activeUsers, stoppingToken);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Presence cleanup failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
