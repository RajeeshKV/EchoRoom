using Chat.Api.DTOs;
using Microsoft.AspNetCore.Http;

namespace Chat.Api.Services;

public interface IChatMediaService
{
    Task<ChatAttachmentDto> SaveAsync(IFormFile file, string kind, CancellationToken cancellationToken);
    Task<ChatAttachmentDto> ValidateAsync(ChatAttachmentDto attachment, CancellationToken cancellationToken);
}
