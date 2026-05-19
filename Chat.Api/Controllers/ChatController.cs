using System.Security.Claims;
using Chat.Api.Cqrs;
using Chat.Api.DTOs;
using Chat.Api.Features.Chat.GetActiveUsers;
using Chat.Api.Features.Chat.GetPrivateMessages;
using Chat.Api.Features.Chat.GetPublicMessages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chat.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ChatController(IRequestDispatcher dispatcher) : ControllerBase
{
    [HttpGet("room")]
    public async Task<ActionResult<IReadOnlyCollection<ChatMessageDto>>> GetGlobalRoomMessages([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var messages = await dispatcher.QueryAsync(new GetPublicMessagesQuery(take), cancellationToken);
        return Ok(messages);
    }

    [HttpGet("private/{username}")]
    public async Task<ActionResult<IReadOnlyCollection<PrivateMessageDto>>> GetPrivateRoomMessages(string username, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var currentUsername = User.Identity?.Name
            ?? User.FindFirst("unique_name")?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrWhiteSpace(currentUsername))
        {
            return Unauthorized();
        }

        var messages = await dispatcher.QueryAsync(new GetPrivateMessagesQuery(currentUsername, username, take), cancellationToken);
        return Ok(messages);
    }

    [HttpGet("active-users")]
    public async Task<ActionResult<IReadOnlyCollection<ActiveUserDto>>> GetActiveUsers(CancellationToken cancellationToken)
    {
        var users = await dispatcher.QueryAsync(new GetActiveUsersQuery(), cancellationToken);
        return Ok(users);
    }
}
