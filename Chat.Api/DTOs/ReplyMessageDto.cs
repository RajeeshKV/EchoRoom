namespace Chat.Api.DTOs;

public class ReplyMessageDto
{
    public Guid MessageId { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? AttachmentKind { get; set; }
}
