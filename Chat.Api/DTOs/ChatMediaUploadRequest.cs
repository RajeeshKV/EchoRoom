using Microsoft.AspNetCore.Http;

namespace Chat.Api.DTOs;

public class ChatMediaUploadRequest
{
    public IFormFile File { get; set; } = default!;
    public string Kind { get; set; } = string.Empty;
}
