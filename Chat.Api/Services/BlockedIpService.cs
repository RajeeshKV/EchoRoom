using Chat.Api.Data;
using Chat.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chat.Api.Services;

public class BlockedIpService(AppDbContext dbContext) : IBlockedIpService
{
    public async Task<bool> IsBlockedAsync(string ipHash, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var expiredBlocks = await dbContext.BlockedIps
            .Where(x => x.BlockedUntil <= now)
            .ToListAsync(cancellationToken);

        if (expiredBlocks.Count > 0)
        {
            dbContext.BlockedIps.RemoveRange(expiredBlocks);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await dbContext.BlockedIps
            .AnyAsync(x => x.IpHash == ipHash && x.BlockedUntil > now, cancellationToken);
    }

    public async Task BlockAsync(string ipHash, DateTime blockedUntil, string reason, CancellationToken cancellationToken)
    {
        var existingBlock = await dbContext.BlockedIps
            .FirstOrDefaultAsync(x => x.IpHash == ipHash, cancellationToken);

        if (existingBlock is null)
        {
            dbContext.BlockedIps.Add(new BlockedIp
            {
                IpHash = ipHash,
                BlockedUntil = blockedUntil,
                Reason = reason
            });
        }
        else
        {
            existingBlock.BlockedUntil = blockedUntil;
            existingBlock.Reason = reason;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
