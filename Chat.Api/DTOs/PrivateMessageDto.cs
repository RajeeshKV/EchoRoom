namespace Chat.Api.DTOs;

public class PrivateMessageDto
{
    public string Sender { get; set; } = string.Empty;
    public string Receiver { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string RoomKey { get; set; } = string.Empty;
}
