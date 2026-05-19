using Chat.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddChatApplication(builder.Configuration);

var app = builder.Build();

app.UseChatApplication();
await app.ApplyDatabaseMigrationsAsync();

app.Run();
