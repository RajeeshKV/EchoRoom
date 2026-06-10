using Chat.Api.Hubs;
using Chat.Api.Middleware;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Chat.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseChatApplication(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseCors("ChatClient");
        app.UseMiddleware<BlockedIpMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/", () => TypedResults.Ok(CreateHealthResponse()));
        app.MapMethods("/", ["HEAD"], () => Results.Ok());
        app.MapGet("/health", () => TypedResults.Ok(CreateHealthResponse()));
        app.MapMethods("/health", ["HEAD"], () => Results.Ok());
        app.MapControllers();
        app.MapHub<ChatHub>("/hubs/chat");

        return app;
    }
    private static object CreateHealthResponse()
    {
        return new
        {
            status = "ok",
            service = "Chat.Api",
            utcTime = DateTime.UtcNow
        };
    }
}
