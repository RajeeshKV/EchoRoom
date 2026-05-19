using Chat.Api.Helpers;
using Chat.Api.Services;

namespace Chat.Api.Middleware;

public class BlockedIpMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IBlockedIpService blockedIpService)
    {
        var ipHash = IpHelper.HashIp(IpHelper.GetIpAddress(context));
        var isBlocked = await blockedIpService.IsBlockedAsync(ipHash, context.RequestAborted);

        if (isBlocked)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "Your IP is temporarily blocked." }, context.RequestAborted);
            return;
        }

        await next(context);
    }
}
