namespace Chat.Api.Entities;

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SenderUsername { get; set; } = string.Empty;
    public string? ReceiverUsername { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public string RoomKey { get; set; } = string.Empty;
    public Guid? ReplyToMessageId { get; set; }
    public string? AttachmentKind { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentContentType { get; set; }
    public long? AttachmentSizeBytes { get; set; }
    public string? AttachmentStorageProvider { get; set; }
    public string? AttachmentPublicId { get; set; }
    public string? AttachmentResourceType { get; set; }
    public DateTime CreatedAt { get; set; }
}
