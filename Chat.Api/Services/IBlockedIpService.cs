namespace Chat.Api.Services;

public interface IBlockedIpService
{
    Task<bool> IsBlockedAsync(string ipHash, CancellationToken cancellationToken);
    Task BlockAsync(string ipHash, DateTime blockedUntil, string reason, CancellationToken cancellationToken);
}
