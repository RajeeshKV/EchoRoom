using Chat.Api.Cqrs;
using Chat.Api.Data;
using Chat.Api.DTOs;
using Chat.Api.Entities;
using Chat.Api.Helpers;
using Chat.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Chat.Api.Features.Auth.Login;

public class LoginCommandHandler(AppDbContext dbContext, IJwtService jwtService, IBlockedIpService blockedIpService)
    : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<LoginResponse> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var normalizedUsername = command.Username.Trim();

        if (!UsernameRules.IsValid(normalizedUsername))
        {
            throw new InvalidOperationException("Username must be 3-20 alphanumeric characters.");
        }

        var ipHash = IpHelper.HashIp(IpHelper.GetIpAddress(command.HttpContext));
        if (await blockedIpService.IsBlockedAsync(ipHash, cancellationToken))
        {
            throw new UnauthorizedAccessException("Your IP is temporarily blocked.");
        }

        var existingOnlineUser = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Username.ToLower() == normalizedUsername.ToLower() && x.IsOnline, cancellationToken);

        if (existingOnlineUser is not null)
        {
            throw new InvalidOperationException("That username is already active.");
        }

        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Username.ToLower() == normalizedUsername.ToLower(), cancellationToken);

        if (existingUser is null)
        {
            dbContext.Users.Add(new User
            {
                Username = normalizedUsername,
                ConnectedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                IsOnline = false,
                IpHash = ipHash
            });
        }
        else
        {
            existingUser.IpHash = ipHash;
            existingUser.LastSeenAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new LoginResponse
        {
            Username = normalizedUsername,
            Token = jwtService.GenerateToken(normalizedUsername)
        };
    }
}
