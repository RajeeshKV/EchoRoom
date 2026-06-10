using Chat.Api.DTOs;
using Chat.Api.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Chat.Api.Services;

public class ChatMediaService(IWebHostEnvironment environment) : IChatMediaService
{
    public async Task<ChatAttachmentDto> SaveAsync(IFormFile file, string kind, CancellationToken cancellationToken)
    {
        kind = kind.Trim().ToLowerInvariant();
        if (!ChatAttachmentRules.IsSupportedKind(kind))
        {
            throw new InvalidOperationException("Unsupported attachment type.");
        }

        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Uploaded file is empty.");
        }

        var maxSizeBytes = ChatAttachmentRules.GetMaxSizeBytes(kind);
        if (file.Length > maxSizeBytes)
        {
            throw new InvalidOperationException($"The {kind} file exceeds the allowed limit of {maxSizeBytes / (1024 * 1024)} MB.");
        }

        if (!ChatAttachmentRules.IsSupportedContentType(kind, file.ContentType))
        {
            throw new InvalidOperationException($"The uploaded file is not a valid {kind}.");
        }

        var uploadsRoot = Path.Combine(environment.ContentRootPath, "wwwroot", "uploads", "chat", kind);
        Directory.CreateDirectory(uploadsRoot);

        var extension = Path.GetExtension(file.FileName);
        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadsRoot, safeFileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return new ChatAttachmentDto
        {
            Kind = kind,
            Url = $"/uploads/chat/{kind}/{safeFileName}",
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length
        };
    }

    public Task<ChatAttachmentDto> ValidateAsync(ChatAttachmentDto attachment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        var kind = attachment.Kind.Trim().ToLowerInvariant();
        if (!ChatAttachmentRules.IsSupportedKind(kind))
        {
            throw new InvalidOperationException("Unsupported attachment type.");
        }

        if (string.IsNullOrWhiteSpace(attachment.Url) || !attachment.Url.StartsWith("/uploads/chat/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Attachment URL is invalid.");
        }

        if (!ChatAttachmentRules.IsSupportedContentType(kind, attachment.ContentType))
        {
            throw new InvalidOperationException($"Attachment content type is invalid for {kind}.");
        }

        if (attachment.SizeBytes <= 0 || attachment.SizeBytes > ChatAttachmentRules.GetMaxSizeBytes(kind))
        {
            throw new InvalidOperationException($"Attachment size is invalid for {kind}.");
        }

        var relativePath = attachment.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(environment.ContentRootPath, "wwwroot", relativePath);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException("Attachment file was not found on the server.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new ChatAttachmentDto
        {
            Kind = kind,
            Url = attachment.Url,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            SizeBytes = attachment.SizeBytes
        });
    }
}
