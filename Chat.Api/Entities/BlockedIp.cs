namespace Chat.Api.Entities;

public class BlockedIp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string IpHash { get; set; } = string.Empty;
    public DateTime BlockedUntil { get; set; }
    public string Reason { get; set; } = string.Empty;
}
