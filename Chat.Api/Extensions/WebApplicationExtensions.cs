using Chat.Api.Data;
using Chat.Api.Hubs;
using Chat.Api.Middleware;
using Microsoft.EntityFrameworkCore;

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
}
