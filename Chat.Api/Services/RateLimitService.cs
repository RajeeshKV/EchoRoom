using System.Collections.Concurrent;

namespace Chat.Api.Services;

public class RateLimitService(IServiceScopeFactory scopeFactory) : IRateLimitService
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StageTwoMute = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StageThreeMute = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StageFourBlock = TimeSpan.FromMinutes(15);
    private const int MaxMessagesPerWindow = 5;

    private readonly ConcurrentDictionary<string, List<DateTime>> _messageWindows = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PenaltyState> _penalties = new(StringComparer.OrdinalIgnoreCase);

    public async Task<RateLimitDecision> EvaluateAsync(string username, string ipHash, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        if (_penalties.TryGetValue(username, out var penalty) && penalty.MutedUntil > now)
        {
            return new RateLimitDecision(false, false, false, "You are temporarily muted.", penalty.MutedUntil, null);
        }

        var timestamps = _messageWindows.AddOrUpdate(
            ipHash,
            _ => [now],
            (_, existing) =>
            {
                lock (existing)
                {
                    existing.RemoveAll(timestamp => now - timestamp > Window);
                    existing.Add(now);
                    return existing;
                }
            });

        var currentCount = timestamps.Count(timestamp => now - timestamp <= Window);
        if (currentCount <= MaxMessagesPerWindow)
        {
            return new RateLimitDecision(true, false, false, null, null, null);
        }

        var updatedPenalty = _penalties.AddOrUpdate(
            username,
            _ => PenaltyState.Warning(now),
            (_, state) => state.Next(now));

        return updatedPenalty.Stage switch
        {
            1 => new RateLimitDecision(true, true, false, "Slow down a bit or you will be muted.", null, null),
            2 => new RateLimitDecision(false, false, false, "You have been muted for 30 seconds.", updatedPenalty.MutedUntil, null),
            3 => new RateLimitDecision(false, false, false, "You have been muted for 5 minutes.", updatedPenalty.MutedUntil, null),
            _ => await BlockIpAsync(ipHash, now, cancellationToken)
        };
    }

    private async Task<RateLimitDecision> BlockIpAsync(string ipHash, DateTime now, CancellationToken cancellationToken)
    {
        var blockedUntil = now.Add(StageFourBlock);
        await using var scope = scopeFactory.CreateAsyncScope();
        var blockedIpService = scope.ServiceProvider.GetRequiredService<IBlockedIpService>();
        await blockedIpService.BlockAsync(ipHash, blockedUntil, "Rate limit abuse", cancellationToken);
        return new RateLimitDecision(false, false, true, "Too many messages. Your IP has been temporarily blocked.", null, blockedUntil);
    }

    private sealed record PenaltyState(int Stage, DateTime? MutedUntil, DateTime UpdatedAt)
    {
        public static PenaltyState Warning(DateTime now) => new(1, null, now);

        public PenaltyState Next(DateTime now) => Stage switch
        {
            1 => new PenaltyState(2, now.Add(StageTwoMute), now),
            2 when MutedUntil <= now => new PenaltyState(3, now.Add(StageThreeMute), now),
            2 => this,
            3 when MutedUntil <= now => new PenaltyState(4, null, now),
            _ => this
        };
    }
}
