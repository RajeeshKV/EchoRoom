using Chat.Api.Cqrs;
using Chat.Api.DTOs;

namespace Chat.Api.Features.Chat.GetPrivateMessages;

public sealed record GetPrivateMessagesQuery(string CurrentUsername, string OtherUsername, int Take) : IQuery<IReadOnlyCollection<PrivateMessageDto>>;
