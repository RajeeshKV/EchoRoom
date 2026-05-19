namespace Chat.Api.DTOs;

public class ChatMessageDto
{
    public string Sender { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}
