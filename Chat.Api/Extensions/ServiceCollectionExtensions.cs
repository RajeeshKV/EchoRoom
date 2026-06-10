using System.Text;
using Chat.Api.BackgroundServices;
using Chat.Api.Cqrs;
using Chat.Api.Data;
using Chat.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Chat.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChatApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddSignalR();
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 55 * 1024 * 1024;
        });
        services.AddCorsPolicy(configuration);
        services.AddDatabase(configuration);
        services.AddJwtAuthentication(configuration);

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IBlockedIpService, BlockedIpService>();
        services.AddSingleton<IChatMediaService, ChatMediaService>();
        services.AddSingleton<IRecentMessageCache, RecentMessageCache>();
        services.AddSingleton<IUserConnectionService, UserConnectionService>();
        services.AddSingleton<IRateLimitService, RateLimitService>();
        services.AddSingleton<IMessagePersistenceQueue, MessagePersistenceQueue>();
        services.AddHostedService<MessageCleanupService>();
        services.AddHostedService<MessagePersistenceService>();
        services.AddHostedService<PresenceCleanupService>();

        services.AddScoped<IRequestDispatcher, RequestDispatcher>();
        services.AddScoped<ICommandHandler<Features.Auth.Login.LoginCommand, DTOs.LoginResponse>, Features.Auth.Login.LoginCommandHandler>();
        services.AddScoped<IQueryHandler<Features.Chat.GetActiveUsers.GetActiveUsersQuery, IReadOnlyCollection<DTOs.ActiveUserDto>>, Features.Chat.GetActiveUsers.GetActiveUsersQueryHandler>();
        services.AddScoped<IQueryHandler<Features.Chat.GetPrivateMessages.GetPrivateMessagesQuery, IReadOnlyCollection<DTOs.PrivateMessageDto>>, Features.Chat.GetPrivateMessages.GetPrivateMessagesQueryHandler>();
        services.AddScoped<IQueryHandler<Features.Chat.GetPublicMessages.GetPublicMessagesQuery, IReadOnlyCollection<DTOs.ChatMessageDto>>, Features.Chat.GetPublicMessages.GetPublicMessagesQueryHandler>();

        services.AddAuthorization();
        return services;
    }

    private static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("ChatClient", policy =>
            {
                var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
                if (origins.Length == 0)
                {
                    origins = ["http://localhost:3000", "http://localhost:5173"];
                }

                policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? configuration["DB_CONNECTION"]
                ?? "Host=localhost;Port=5432;Database=chatroom;Username=postgres;Password=postgres";

            options.UseNpgsql(connectionString);
        });

        return services;
    }

    private static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSecret = configuration["JWT_SECRET"] ?? configuration["Jwt:Secret"] ?? "replace-this-local-dev-secret-with-32-characters";
        var jwtIssuer = configuration["JWT_ISSUER"] ?? configuration["Jwt:Issuer"] ?? "Chat.Api";
        var jwtAudience = configuration["JWT_AUDIENCE"] ?? configuration["Jwt:Audience"] ?? "Chat.Client";

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments("/hubs/chat"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }
}
