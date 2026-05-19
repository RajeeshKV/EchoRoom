namespace Chat.Api.Entities;

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SenderUsername { get; set; } = string.Empty;
    public string? ReceiverUsername { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public string RoomKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
