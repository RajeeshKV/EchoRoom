namespace Chat.Api.DTOs;

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ChatAttachmentDto? Attachment { get; set; }
    public ReplyMessageDto? ReplyTo { get; set; }
    public DateTime SentAt { get; set; }
}
