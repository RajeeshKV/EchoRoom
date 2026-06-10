using Chat.Api.DTOs;
using Chat.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chat.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chat/media")]
public class ChatMediaController(
    IChatMediaService chatMediaService,
    ILogger<ChatMediaController> logger) : ControllerBase
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(55 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 55 * 1024 * 1024)]
    public async Task<ActionResult<ChatMediaUploadResponse>> Upload([FromForm] ChatMediaUploadRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Media upload request received. Username={Username}, Kind={Kind}, FileName={FileName}, ContentType={ContentType}, SizeBytes={SizeBytes}.",
            User.Identity?.Name ?? "<unknown>",
            request.Kind,
            request.File?.FileName ?? "<none>",
            request.File?.ContentType ?? "<none>",
            request.File?.Length ?? 0);

        try
        {
            if (request.File is null)
            {
                throw new InvalidOperationException("Uploaded file is required.");
            }

            var attachment = await chatMediaService.SaveAsync(request.File, request.Kind, cancellationToken);
            logger.LogInformation(
                "Media upload request completed. Username={Username}, Kind={Kind}, Url={Url}.",
                User.Identity?.Name ?? "<unknown>",
                attachment.Kind,
                attachment.Url);

            return Ok(new ChatMediaUploadResponse
            {
                Attachment = attachment
            });
        }
        catch (CloudinaryMediaException exception)
        {
            logger.LogWarning(
                exception,
                "Media upload request rejected by Cloudinary. Username={Username}, Kind={Kind}, StatusCode={StatusCode}, Message={Message}.",
                User.Identity?.Name ?? "<unknown>",
                request.Kind,
                (int)exception.StatusCode,
                exception.Message);
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(
                exception,
                "Media upload request failed validation. Username={Username}, Kind={Kind}, Message={Message}.",
                User.Identity?.Name ?? "<unknown>",
                request.Kind,
                exception.Message);
            return BadRequest(new { message = exception.Message });
        }
    }
}
