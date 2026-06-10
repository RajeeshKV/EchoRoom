using System.Text.Json.Serialization;

namespace Chat.Api.DTOs;

public class ChatAttachmentDto
{
    [JsonIgnore]
    public string Provider { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    [JsonIgnore]
    public string PublicId { get; set; } = string.Empty;

    [JsonIgnore]
    public string ResourceType { get; set; } = string.Empty;
}
