namespace Chat.Api.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsOnline { get; set; }
    public string IpHash { get; set; } = string.Empty;
}
