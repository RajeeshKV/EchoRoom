namespace Chat.Api.DTOs;

public class SendChatMessageRequest
{
    public string? Message { get; set; }
    public Guid? ReplyToMessageId { get; set; }
    public ChatAttachmentDto? Attachment { get; set; }
}
