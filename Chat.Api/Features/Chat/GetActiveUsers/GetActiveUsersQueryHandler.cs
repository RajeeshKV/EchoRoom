using Chat.Api.Cqrs;
using Chat.Api.DTOs;
using Chat.Api.Services;

namespace Chat.Api.Features.Chat.GetActiveUsers;

public class GetActiveUsersQueryHandler(IUserConnectionService userConnectionService)
    : IQueryHandler<GetActiveUsersQuery, IReadOnlyCollection<ActiveUserDto>>
{
    public Task<IReadOnlyCollection<ActiveUserDto>> HandleAsync(GetActiveUsersQuery query, CancellationToken cancellationToken)
        => userConnectionService.GetActiveUsersAsync(cancellationToken);
}
