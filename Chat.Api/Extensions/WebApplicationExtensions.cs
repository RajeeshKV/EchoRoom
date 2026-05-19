using Chat.Api.Data;
using Chat.Api.Hubs;
using Chat.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Chat.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseChatApplication(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
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

    public static async Task ApplyDatabaseMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
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
