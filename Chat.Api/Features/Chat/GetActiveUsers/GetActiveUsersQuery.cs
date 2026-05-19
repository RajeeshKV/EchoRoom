using Chat.Api.Cqrs;
using Chat.Api.DTOs;

namespace Chat.Api.Features.Chat.GetActiveUsers;

public sealed record GetActiveUsersQuery() : IQuery<IReadOnlyCollection<ActiveUserDto>>;
