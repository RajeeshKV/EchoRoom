using System.Collections.Concurrent;
using Chat.Api.Data;
using Chat.Api.DTOs;
using Chat.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chat.Api.Services;

public class UserConnectionService(IServiceScopeFactory scopeFactory) : IUserConnectionService
{
    private readonly ConcurrentDictionary<string, string> _activeConnections = new(StringComparer.OrdinalIgnoreCase);
    public string GlobalRoomName => "global";

    public async Task<string?> RegisterConnectionAsync(string username, string connectionId, CancellationToken cancellationToken)
    {
        string? displacedConnectionId = null;
        var hadExistingConnection = _activeConnections.TryGetValue(username, out var previousConnectionId);
        _activeConnections[username] = connectionId;

        if (hadExistingConnection && !string.Equals(previousConnectionId, connectionId, StringComparison.Ordinal))
        {
            displacedConnectionId = previousConnectionId;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Username == username, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Username = username,
                ConnectedAt = now,
                LastSeenAt = now,
                IsOnline = true
            };

            dbContext.Users.Add(user);
        }
        else
        {
            user.IsOnline = true;
            user.LastSeenAt = now;
        }

        dbContext.UserConnections.Add(new UserConnection
        {
            Username = username,
            ConnectionId = connectionId,
            ConnectedAt = now
        });

        if (hadExistingConnection)
        {
            var oldConnections = await dbContext.UserConnections
                .Where(x => x.Username == username && x.ConnectionId != connectionId)
                .ToListAsync(cancellationToken);

            if (oldConnections.Count > 0)
            {
                dbContext.UserConnections.RemoveRange(oldConnections);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return displacedConnectionId;
    }

    public async Task RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = await dbContext.UserConnections
            .FirstOrDefaultAsync(x => x.ConnectionId == connectionId, cancellationToken);

        if (connection is null)
        {
            return;
        }

        dbContext.UserConnections.Remove(connection);
        await dbContext.SaveChangesAsync(cancellationToken);

        var hasOtherConnections = await dbContext.UserConnections
            .AnyAsync(x => x.Username == connection.Username, cancellationToken);

        if (!hasOtherConnections)
        {
            _activeConnections.TryRemove(connection.Username, out _);

            var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Username == connection.Username, cancellationToken);
            if (user is not null)
            {
                user.IsOnline = false;
                user.LastSeenAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    public async Task TouchUserAsync(string username, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Username == username, cancellationToken);
        if (user is null)
        {
            return;
        }

        user.LastSeenAt = DateTime.UtcNow;
        user.IsOnline = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> MarkStaleUsersOfflineAsync(TimeSpan staleAfter, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = DateTime.UtcNow.Subtract(staleAfter);

        var staleUsers = await dbContext.Users
            .Where(x => x.IsOnline && x.LastSeenAt < cutoff)
            .ToListAsync(cancellationToken);

        if (staleUsers.Count == 0)
        {
            return Array.Empty<string>();
        }

        var staleUsernames = staleUsers
            .Select(x => x.Username)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var user in staleUsers)
        {
            user.IsOnline = false;
        }

        var staleConnections = await dbContext.UserConnections
            .Where(x => staleUsernames.Contains(x.Username))
            .ToListAsync(cancellationToken);

        if (staleConnections.Count > 0)
        {
            dbContext.UserConnections.RemoveRange(staleConnections);
        }

        foreach (var username in staleUsernames)
        {
            _activeConnections.TryRemove(username, out _);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return staleUsernames;
    }

    public async Task<IReadOnlyCollection<ActiveUserDto>> GetActiveUsersAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Users
            .Where(x => x.IsOnline)
            .OrderBy(x => x.Username)
            .Select(x => new ActiveUserDto { Username = x.Username })
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> GetConnectionIdAsync(string username, CancellationToken cancellationToken)
    {
        if (_activeConnections.TryGetValue(username, out var connectionId))
        {
            return connectionId;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        connectionId = await dbContext.UserConnections
            .Where(x => x.Username == username)
            .OrderByDescending(x => x.ConnectedAt)
            .Select(x => x.ConnectionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(connectionId))
        {
            _activeConnections[username] = connectionId;
        }

        return connectionId;
    }

    public string GetPrivateRoomKey(string firstUsername, string secondUsername)
    {
        var ordered = new[] { firstUsername.Trim(), secondUsername.Trim() }
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

        return $"dm:{string.Join(':', ordered)}";
    }
}
