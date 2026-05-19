using Chat.Api.Cqrs;
using Chat.Api.DTOs;
using Chat.Api.Features.Auth.Login;
using Microsoft.AspNetCore.Mvc;

namespace Chat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IRequestDispatcher dispatcher) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await dispatcher.SendAsync(new LoginCommand(request.Username, HttpContext), cancellationToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, exception.Message);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("already active", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
