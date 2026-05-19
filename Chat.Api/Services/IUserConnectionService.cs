using Chat.Api.DTOs;

namespace Chat.Api.Services;

public interface IUserConnectionService
{
    string GlobalRoomName { get; }
    Task<string?> RegisterConnectionAsync(string username, string connectionId, CancellationToken cancellationToken);
    Task RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken);
    Task TouchUserAsync(string username, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> MarkStaleUsersOfflineAsync(TimeSpan staleAfter, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ActiveUserDto>> GetActiveUsersAsync(CancellationToken cancellationToken);
    Task<string?> GetConnectionIdAsync(string username, CancellationToken cancellationToken);
    string GetPrivateRoomKey(string firstUsername, string secondUsername);
}
