using Chat.Api.DTOs;
using Chat.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chat.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chat/media")]
public class ChatMediaController(IChatMediaService chatMediaService) : ControllerBase
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(55 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 55 * 1024 * 1024)]
    public async Task<ActionResult<ChatMediaUploadResponse>> Upload([FromForm] ChatMediaUploadRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var attachment = await chatMediaService.SaveAsync(request.File, request.Kind, cancellationToken);
            return Ok(new ChatMediaUploadResponse
            {
                Attachment = attachment
            });
        }
        catch (CloudinaryMediaException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
