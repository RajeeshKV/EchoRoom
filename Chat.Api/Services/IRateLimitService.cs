namespace Chat.Api.Services;

public interface IRateLimitService
{
    Task<RateLimitDecision> EvaluateAsync(string username, string ipHash, CancellationToken cancellationToken);
}

public sealed record RateLimitDecision(
    bool IsAllowed,
    bool ShouldWarn,
    bool ShouldBlockIp,
    string? Message,
    DateTime? MutedUntil,
    DateTime? BlockedUntil);
